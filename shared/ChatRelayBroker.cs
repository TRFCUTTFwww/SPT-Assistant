using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
namespace AssistantChat;
// Bounded, in-memory, opaque byte relay. No plaintext or command processing.
public sealed class ChatRelayBroker:IDisposable {
 sealed class Client(ChatPeer peer,X509Certificate2 cert){public ChatPeer Peer=peer;public X509Certificate2 Cert=cert;public DateTime Seen=DateTime.UtcNow;public long Sequence,Ack;public int Bytes;public readonly Queue<ChatPacket> Packets=new();public readonly Dictionary<string,long> Nonces=new();public int Requests;public DateTime Window=DateTime.UtcNow;}
 readonly object gate=new();readonly Dictionary<string,Client> clients=new();readonly Func<string,string,string[]> policy;
 public ChatRelayBroker(Func<string,string,string[]> policy)=>this.policy=policy;
 public ChatPoll Handle(ChatRequest r,string profile,string name){lock(gate){try{
  foreach(var stale in clients.Where(p=>(DateTime.UtcNow-p.Value.Seen).TotalSeconds>60).Select(p=>p.Key).ToArray()){clients[stale].Cert.Dispose();clients.Remove(stale);}
  if(r==null||string.IsNullOrWhiteSpace(profile)||profile.Length>80||name==null||name.Length>128)throw new ArgumentException("Unknown profile");
  if(r.Action==null||r.Action.Length>16||r.Connection==null||r.Connection.Length!=32||!r.Connection.All(Uri.IsHexDigit)||r.Certificate==null||r.Certificate.Length>4096||r.Data==null||r.Data.Length>44000||r.To==null||r.To.Length>32||r.Permission==null||r.Permission.Length>80||r.Target==null||r.Target.Length>160||r.Signature==null||r.Signature.Length>256||r.Nonce==null||r.Nonce.Length!=32)throw new ArgumentException("Invalid request size");
  if(r.Action=="register"&&!clients.ContainsKey(r.Connection)){
   if(clients.Count>=64||clients.Values.Count(c=>c.Peer.Profile==profile)>=2)throw new ArgumentException("Connection limit; wait 60 seconds after reconnecting");
   using var cert=ChatCertificates.Read(r.Certificate);if(!r.Verify(cert))throw new ArgumentException("Invalid identity signature");
   // Validate policy before allocating a connection, including denied identities.
   if(!policy(profile,ChatCertificates.Fingerprint(cert)).Contains("chat.send"))throw new ArgumentException("Permission denied");
   clients.Add(r.Connection,new(new(r.Connection,profile,name,r.Certificate),ChatCertificates.Read(r.Certificate)));
  }
  if(!clients.TryGetValue(r.Connection,out var client)||client.Peer.Profile!=profile||r.Certificate!=client.Peer.Certificate||!r.Verify(client.Cert))throw new ArgumentException("Identity verification failed");
  if((DateTime.UtcNow-client.Window).TotalSeconds>=1){client.Window=DateTime.UtcNow;client.Requests=0;}if(++client.Requests>100)throw new ArgumentException("Rate limit");
  long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();foreach(var nonce in client.Nonces.Where(n=>n.Value<now-120).Select(n=>n.Key).ToArray())client.Nonces.Remove(nonce);
  if(!client.Nonces.TryAdd(r.Nonce,r.Time))throw new ArgumentException("Replay rejected");
  client.Seen=DateTime.UtcNow;var permissions=policy(profile,ChatCertificates.Fingerprint(client.Cert));if(!permissions.Contains("chat.send"))throw new ArgumentException("Permission revoked");
  if(r.After<client.Ack||r.After>client.Sequence)throw new ArgumentException("Invalid acknowledgement");client.Ack=r.After;
  while(client.Packets.TryPeek(out var packet)&&packet.Sequence<=r.After){client.Packets.Dequeue();client.Bytes-=packet.Data.Length;}
  switch(r.Action){
   case "register":case "poll":break;
   case "trust":if(!clients.ContainsKey(r.To)||r.To==r.Connection)throw new ArgumentException("Recipient offline");client.Peer=client.Peer with{Trusted=(client.Peer.Trusted??Array.Empty<string>()).Append(r.To).Distinct().Where(clients.ContainsKey).ToArray()};break;
   case "send":
    if(!clients.TryGetValue(r.To,out var recipient)||r.To==r.Connection)throw new ArgumentException("Recipient offline");
    if(!policy(recipient.Peer.Profile,ChatCertificates.Fingerprint(recipient.Cert)).Contains("chat.send"))throw new ArgumentException("Recipient permission revoked");
    if(client.Peer.Trusted?.Contains(r.To)!=true||recipient.Peer.Trusted?.Contains(r.Connection)!=true)throw new ArgumentException("Both peers must verify their identities first");
    byte[] bytes=Convert.FromBase64String(r.Data);if(bytes.Length is <1 or >32768)throw new ArgumentException("Invalid record size");
    if(recipient.Packets.Count>=256||recipient.Bytes+r.Data.Length>2097152)throw new ArgumentException("Recipient queue full; reconnect required");
    recipient.Packets.Enqueue(new(++recipient.Sequence,r.Connection,r.To,r.Data));recipient.Bytes+=r.Data.Length;break;
   case "leave":client.Cert.Dispose();clients.Remove(r.Connection);return new(r.Connection,Array.Empty<ChatPeer>(),Array.Empty<ChatPacket>(),Array.Empty<string>());
   default:throw new ArgumentException("Unknown chat operation");
  }
  return new(r.Connection,clients.Values.Where(c=>c!=client).Select(c=>c.Peer).ToArray(),client.Packets.Take(64).ToArray(),permissions);
 }catch(Exception e) when(e is ArgumentException or CryptographicException or FormatException or JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException){return new(r?.Connection??"",Array.Empty<ChatPeer>(),Array.Empty<ChatPacket>(),Array.Empty<string>(),e is ArgumentException?e.Message:"Invalid chat identity or configuration");}}}
 public void Dispose(){lock(gate){foreach(var c in clients.Values)c.Cert.Dispose();clients.Clear();}}
}
