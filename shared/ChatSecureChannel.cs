using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace AssistantChat;
public sealed class ChatSecureChannel:IDisposable {
 readonly SslStream tls;readonly SemaphoreSlim writing=new(1,1);readonly CancellationTokenSource stop=new();public readonly ChatRelayStream Relay;public Task Ready{get;}public string Fingerprint{get;}
 public ChatSecureChannel(X509Certificate2 identity,string expectedFingerprint,bool initiator,Func<byte[],CancellationToken,Task> send,Action<ChatMessage> receive,Action<string> failed,string localContext="",string expectedContext=""){
  Fingerprint=expectedFingerprint;Relay=new(send);
  tls=new SslStream(Relay,false,(_,cert,_,_)=>{if(cert==null)return false;using var remote=ChatCertificates.Read(Convert.ToBase64String(cert.GetRawCertData()));return ChatCertificates.Fingerprint(remote)==expectedFingerprint;});
  Ready=Authenticate(identity,initiator,localContext,expectedContext);_ = ReadLoop(receive,failed);
 }
 async Task Authenticate(X509Certificate2 identity,bool initiator,string localContext,string expectedContext){using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(40));
  // TLS 1.3 only, fresh ECDHE per connection, no resumption or early data.
  if(initiator)await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="assistant-peer",EnabledSslProtocols=SslProtocols.Tls13,AllowTlsResume=false,CertificateRevocationCheckMode=X509RevocationMode.NoCheck,ClientCertificates=new X509CertificateCollection{identity},LocalCertificateSelectionCallback=(_,_,_,_,_)=>identity},timeout.Token);
  else await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=identity,ClientCertificateRequired=true,EnabledSslProtocols=SslProtocols.Tls13,AllowTlsResume=false,CertificateRevocationCheckMode=X509RevocationMode.NoCheck},timeout.Token);
  if(tls.SslProtocol!=SslProtocols.Tls13||!tls.IsMutuallyAuthenticated)throw new AuthenticationException("Mutual TLS 1.3 required");
  // Bind the pinned identity to both profile IDs and ephemeral relay connections
  // inside TLS. Relay metadata alone cannot authenticate these bindings.
  byte[] context=System.Text.Encoding.UTF8.GetBytes(localContext);if(context.Length>1024)throw new AuthenticationException("Invalid context");byte[] size=new byte[4];BinaryPrimitives.WriteInt32BigEndian(size,context.Length);await tls.WriteAsync(size,timeout.Token);await tls.WriteAsync(context,timeout.Token);await tls.FlushAsync(timeout.Token);await tls.ReadExactlyAsync(size,timeout.Token);int count=BinaryPrimitives.ReadInt32BigEndian(size);if(count is <0 or >1024)throw new AuthenticationException("Invalid context");byte[] remote=new byte[count];await tls.ReadExactlyAsync(remote,timeout.Token);if(System.Text.Encoding.UTF8.GetString(remote)!=expectedContext)throw new AuthenticationException("Profile or connection binding mismatch");
 }
 public async Task Send(ChatMessage message){byte[] payload=JsonSerializer.SerializeToUtf8Bytes(message);if(payload.Length>16384)throw new ArgumentException("Message too long");await Ready;await writing.WaitAsync(stop.Token);try{byte[] length=new byte[4];BinaryPrimitives.WriteInt32BigEndian(length,payload.Length);await tls.WriteAsync(length,stop.Token);await tls.WriteAsync(payload,stop.Token);await tls.FlushAsync(stop.Token);}finally{writing.Release();}}
 async Task ReadLoop(Action<ChatMessage> receive,Action<string> failed){try{await Ready;while(!stop.IsCancellationRequested){byte[] length=new byte[4];await tls.ReadExactlyAsync(length,stop.Token);int n=BinaryPrimitives.ReadInt32BigEndian(length);if(n is <1 or >16384)throw new InvalidDataException("Invalid encrypted message size");byte[] data=new byte[n];await tls.ReadExactlyAsync(data,stop.Token);var message=JsonSerializer.Deserialize<ChatMessage>(data)??throw new InvalidDataException("Invalid message");if(message.Kind==null||message.Kind.Length>24||message.Text==null||message.Text.Length>2000)throw new InvalidDataException("Invalid message fields");receive(message);}}catch(Exception e){if(!stop.IsCancellationRequested)failed(e is AuthenticationException?"身份核验或 TLS 1.3 握手失败。":"加密连接已中断，需要重新连接。");Dispose();}}
 public void Dispose(){if(stop.IsCancellationRequested)return;stop.Cancel();tls.Dispose();Relay.Dispose();}
}
