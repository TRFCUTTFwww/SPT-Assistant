using AssistantChat;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
internal static class HttpRelayIntegration {
 internal static async Task Run(string session){if(session.Length!=24||!session.All(Uri.IsHexDigit))throw new ArgumentException("Expected SPT profile ID");using var a=new Endpoint(session);using var b=new Endpoint(session);
  try{await a.Exchange("register");await b.Exchange("register");await a.Exchange("trust",b.Id);await b.Exchange("trust",a.Id);a.Open(b,true);b.Open(a,false);
   using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(30));var pump=Task.Run(async()=>{while(!cts.IsCancellationRequested){await a.Exchange("poll");await b.Exchange("poll");await Task.Delay(50,cts.Token);}},cts.Token);
   try{await Task.WhenAll(a.Channel!.Ready,b.Channel!.Ready).WaitAsync(cts.Token);await a.Channel.Send(new("chat","HTTP TLS integration 消息"));if((await b.Inbox.Task.WaitAsync(cts.Token)).Text!="HTTP TLS integration 消息")throw new Exception("HTTP message mismatch");await b.Channel.Send(new("whisper","HTTP TLS return"));if((await a.Inbox.Task.WaitAsync(cts.Token)).Text!="HTTP TLS return")throw new Exception("HTTP return mismatch");Console.WriteLine("PASS live SPT signed relay endpoint, mutual TLS 1.3 and bidirectional messages");}finally{cts.Cancel();try{await pump;}catch(OperationCanceledException){}}
  }finally{try{await a.Exchange("leave");await b.Exchange("leave");}catch{}}
 }
 sealed class Endpoint(string session):IDisposable {
  public readonly string Id=Guid.NewGuid().ToString("N");public readonly X509Certificate2 Identity=ChatCertificates.Create();public ChatSecureChannel? Channel;public readonly TaskCompletionSource<ChatMessage> Inbox=new(TaskCreationOptions.RunContinuationsAsynchronously);readonly SemaphoreSlim request=new(1,1);long after;
  public async Task Exchange(string action,string to="",byte[]? bytes=null){await request.WaitAsync();try{var r=new ChatRequest{Action=action,Connection=Id,Certificate=Convert.ToBase64String(Identity.RawData),To=to,Data=bytes==null?"":Convert.ToBase64String(bytes),After=after};r.Sign(Identity);byte[] data=await Post(JsonSerializer.Serialize(r));string body;if(data.Length>1&&data[0]==0x78){using var zlib=new ZLibStream(new MemoryStream(data),CompressionMode.Decompress);using var reader=new StreamReader(zlib);body=await reader.ReadToEndAsync();}else body=Encoding.UTF8.GetString(data);if(body.Length==0)throw new IOException("Empty SPT HTTP response");using var doc=JsonDocument.Parse(body);var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new Exception("SPT endpoint error: "+body);if(root.TryGetProperty("data",out var nested))root=nested;var poll=root.Deserialize<ChatPoll>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new Exception("missing poll");if(poll.Error.Length>0)throw new Exception(poll.Error);foreach(var packet in poll.Packets){if(packet.Sequence<=after)continue;if(packet.Sequence!=after+1)throw new Exception("HTTP cursor gap");if(Channel==null||!Channel.Relay.Feed(Convert.FromBase64String(packet.Data)))throw new Exception("HTTP stream not ready");after=packet.Sequence;}}finally{request.Release();}}
  public void Open(Endpoint peer,bool initiator){Channel=new(Identity,ChatCertificates.Fingerprint(peer.Identity),initiator,(bytes,_)=>Exchange("send",peer.Id,bytes),m=>Inbox.TrySetResult(m),e=>Inbox.TrySetException(new Exception(e)),Id+peer.Id,peer.Id+Id);}
  // Optional local integration test only; curl matches SPT's HTTP/1.1 framing.
  async Task<byte[]> Post(string json){string file=Path.GetTempFileName();try{
   byte[] payload=Encoding.UTF8.GetBytes(json);byte[] frame=new byte[payload.Length+4];System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(frame,payload.Length);payload.CopyTo(frame,4);int offset=frame.Length%2731;for(int i=frame.Length-1;i>=1;i--){int j=(int)(((1624453UL*(417645UL+(ulong)(i+offset))+1023920427UL)%2396496269UL)%(uint)i);(frame[i],frame[j])=(frame[j],frame[i]);}await File.WriteAllBytesAsync(file,frame);
   var start=new ProcessStartInfo("curl.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   foreach(string arg in new[]{"--insecure","--silent","--show-error","--fail","--max-time","15","--http1.1","--noproxy","*","-X","POST","https://127.0.0.1:6969/local/assistant/chat","-H","Content-Type: application/json","-H","requestcompressed: 0","-H","responsecompressed: 0","-H","Cookie: PHPSESSID="+session,"--data-binary","@"+file})start.ArgumentList.Add(arg);
   using var process=Process.Start(start)??throw new IOException("curl could not start");using var output=new MemoryStream();var read=process.StandardOutput.BaseStream.CopyToAsync(output);var error=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();await read;if(process.ExitCode!=0)throw new IOException("curl: "+await error);return output.ToArray();
  }finally{File.Delete(file);}}
  public void Dispose(){Channel?.Dispose();Identity.Dispose();}
 }
}
