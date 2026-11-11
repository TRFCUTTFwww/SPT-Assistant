using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistantChat;
using SPTushonka.Common.Http;
namespace LocalSpt5Esp;
internal sealed class AssistantChatClient:IDisposable {
 readonly X509Certificate2 identity;readonly string certificate,connection=Guid.NewGuid().ToString("N");readonly CancellationTokenSource stop=new();readonly SemaphoreSlim requests=new(1,1);readonly object gate=new();
 readonly Dictionary<string,ChatSecureChannel> channels=new();readonly Dictionary<string,string> trusted;readonly ConcurrentQueue<(string Peer,ChatMessage Message)> received=new();readonly ConcurrentQueue<string> notices=new();readonly string trustPath;
 readonly HashSet<string> announced=new();readonly string profileId;int pendingMessages,receivedCount,disposed;
 ChatPeer[] peers=Array.Empty<ChatPeer>();long after;bool registered;string[] permissions=Array.Empty<string>();
 internal ChatPeer[] Peers{get{lock(gate)return peers.ToArray();}}internal string Fingerprint=>ChatCertificates.Fingerprint(identity);internal string[] Permissions{get{lock(gate)return permissions.ToArray();}}
 internal AssistantChatClient(string profile){
  profileId=profile;
  string home=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","chat",Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(profile)))[..24]);Directory.CreateDirectory(home);trustPath=Path.Combine(home,"trusted.json");
  string path=Path.Combine(home,"identity.pfx");byte[] secret;
  if(File.Exists(path)){secret=ChatIdentityProtection.Transform(File.ReadAllBytes(path),false);try{identity=X509CertificateLoader.LoadPkcs12(secret,null,X509KeyStorageFlags.DefaultKeySet|X509KeyStorageFlags.Exportable);}finally{CryptographicOperations.ZeroMemory(secret);}}
  else{identity=ChatCertificates.Create();secret=identity.Export(X509ContentType.Pfx);try{File.WriteAllBytes(path,ChatIdentityProtection.Transform(secret,true));}finally{CryptographicOperations.ZeroMemory(secret);}}
  certificate=Convert.ToBase64String(identity.RawData);trusted=File.Exists(trustPath)?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(trustPath))??new():new();_ = Run();
 }
 async Task<ChatPoll> Request(string action,string to="",byte[]? data=null){
  await requests.WaitAsync(stop.Token);try{
   var r=new ChatRequest{Action=action,Connection=connection,Certificate=certificate,After=after,To=to,Data=data==null?"":Convert.ToBase64String(data)};r.Sign(identity);
   string response=await RequestHandler.PostJsonAsync("/local/assistant/chat",JsonSerializer.Serialize(r)).WaitAsync(TimeSpan.FromSeconds(15),stop.Token);
   using var doc=JsonDocument.Parse(response);var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new IOException("Chat server unavailable");if(root.TryGetProperty("data",out var body))root=body;
   var poll=root.Deserialize<ChatPoll>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("Invalid chat response");if(poll.Error.Length>0)throw new IOException(poll.Error);
   lock(gate){peers=poll.Peers;permissions=poll.Permissions;}
   // Every response may contain unacknowledged records. Deliver exactly once.
   foreach(var packet in poll.Packets.OrderBy(p=>p.Sequence)){
    if(packet.Sequence<=after)continue;if(packet.Sequence!=after+1||packet.To!=connection)throw new IOException("Chat stream gap");
    lock(gate){var peer=peers.FirstOrDefault(p=>p.Connection==packet.From);if(peer!=null&&Trusted(peer)&&Mutual(peer)){var ch=Channel(peer);if(!ch.Relay.Feed(Convert.FromBase64String(packet.Data)))throw new IOException("Chat stream overflow");}}after=packet.Sequence;
   }
   return poll;
  }finally{requests.Release();}
 }
 bool Trusted(ChatPeer peer){using var cert=ChatCertificates.Read(peer.Certificate);return trusted.TryGetValue(peer.Profile,out var pin)&&pin==ChatCertificates.Fingerprint(cert);}
 bool Mutual(ChatPeer peer)=>announced.Contains(peer.Connection)&&peer.Trusted?.Contains(connection)==true;
 ChatSecureChannel Channel(ChatPeer peer){
  if(channels.TryGetValue(peer.Connection,out var existing))return existing;
  using var cert=ChatCertificates.Read(peer.Certificate);var channel=new ChatSecureChannel(identity,ChatCertificates.Fingerprint(cert),string.CompareOrdinal(connection,peer.Connection)<0,(bytes,ct)=>SendRecord(peer.Connection,bytes),msg=>{if(Interlocked.Increment(ref receivedCount)>512){Interlocked.Decrement(ref receivedCount);throw new IOException("Receive queue overflow");}received.Enqueue((peer.Profile,msg));},notice=>notices.Enqueue(peer.Name+": "+notice),JsonSerializer.Serialize(new[]{profileId,connection,peer.Profile,peer.Connection}),JsonSerializer.Serialize(new[]{peer.Profile,peer.Connection,profileId,connection}));channels.Add(peer.Connection,channel);return channel;
 }
 async Task SendRecord(string to,byte[] bytes)=>await Request("send",to,bytes);
 async Task Run(){try{
  await Request("register");registered=true;notices.Enqueue("加密聊天中继已连接。/identity 查看并核验指纹。");
  while(!stop.IsCancellationRequested){ChatPeer[] announce;lock(gate)announce=peers.Where(p=>Trusted(p)&&!announced.Contains(p.Connection)).ToArray();foreach(var peer in announce){lock(gate)announced.Add(peer.Connection);await Request("trust",peer.Connection);}lock(gate){foreach(string id in channels.Keys.Where(id=>!peers.Any(p=>p.Connection==id)).ToArray()){channels[id].Dispose();channels.Remove(id);}foreach(var peer in peers)if(Trusted(peer)&&Mutual(peer))Channel(peer);}
   await Task.Delay(350,stop.Token);await Request("poll");
  }
 }catch(OperationCanceledException){}catch{if(!stop.IsCancellationRequested){notices.Enqueue("加密聊天连接中断或服务端组件不可用。使用 /chat reconnect 重连；没有明文回退。");stop.Cancel();lock(gate){registered=false;foreach(var channel in channels.Values)channel.Dispose();channels.Clear();}}}}
 internal string Verify(string target,string fingerprint){lock(gate){var found=peers.Where(p=>p.Profile==target||p.Name.Equals(target,StringComparison.OrdinalIgnoreCase)).ToArray();if(found.Length!=1)throw new ArgumentException("身份目标不存在或重名，请使用档案 ID。");var peer=found[0];using var cert=ChatCertificates.Read(peer.Certificate);string actual=ChatCertificates.Fingerprint(cert);string supplied=fingerprint.Replace(":","").ToUpperInvariant();if(supplied!=actual)throw new ArgumentException("指纹不一致，未保存信任。");var next=new Dictionary<string,string>(trusted){[peer.Profile]=actual};string temp=trustPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(next));File.Move(temp,trustPath,true);trusted[peer.Profile]=actual;return "已核验 "+peer.Name+"；等待对方核验你的指纹。";}}
 internal string Describe(ChatPeer p){using var cert=ChatCertificates.Read(p.Certificate);lock(gate)return p.Name+" ["+p.Profile+"] "+(Trusted(p)?"verified":"UNVERIFIED")+" "+ChatCertificates.Fingerprint(cert);}
 internal async Task<int> Send(string text,string? target=null,string kind="chat"){
  if(text.Length is <1 or >2000)throw new ArgumentException("消息长度须为 1～2000 个字符。");if(Interlocked.Increment(ref pendingMessages)>8){Interlocked.Decrement(ref pendingMessages);throw new ArgumentException("发送队列已满，请等待。");}try{ChatSecureChannel[] selected;
  lock(gate){if(!registered)throw new ArgumentException("聊天尚未连接。");var destinations=peers.Where(p=>target==null||p.Profile==target||p.Name.Equals(target,StringComparison.OrdinalIgnoreCase)).ToArray();if(target!=null&&destinations.Length!=1)throw new ArgumentException("私信目标不存在或重名，请用档案 ID。");if(destinations.Length==0)throw new ArgumentException("当前没有其他在线模组用户。");if(destinations.Any(p=>!Trusted(p)||!Mutual(p)))throw new ArgumentException("存在未完成双方核验的收件人。请先 /identity verify；未发送消息。");selected=destinations.Select(Channel).ToArray();}
  await Task.WhenAll(selected.Select(ch=>ch.Send(new ChatMessage(kind,text)))).WaitAsync(TimeSpan.FromSeconds(45),stop.Token);return selected.Length;
  }finally{Interlocked.Decrement(ref pendingMessages);}
 }
 internal bool TryReceive(out (string Peer,ChatMessage Message) item){if(!received.TryDequeue(out item))return false;Interlocked.Decrement(ref receivedCount);return true;}internal bool TryNotice(out string notice)=>notices.TryDequeue(out notice!);
 public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Cancel();lock(gate){foreach(var channel in channels.Values)channel.Dispose();channels.Clear();} // In-flight requests may still use identity; dispose after their gate drains.
  _=Task.Run(async()=>{await requests.WaitAsync();try{identity.Dispose();}finally{requests.Release();}});
 }
}
