using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
namespace AssistantChat;
public sealed record ChatPeer(string Connection,string Profile,string Name,string Certificate,string[]? Trusted=null);
public sealed record ChatPacket(long Sequence,string From,string To,string Data);
public sealed record ChatPoll(string Self,ChatPeer[] Peers,ChatPacket[] Packets,string[] Permissions,string Error="");
public class ChatRequest {
 public string Action{get;set;}="poll";public string Connection{get;set;}="";public string Certificate{get;set;}="";public string To{get;set;}="";public string Data{get;set;}="";public string Permission{get;set;}="";public string Target{get;set;}="";public long After{get;set;}public long Time{get;set;}public string Nonce{get;set;}="";public string Signature{get;set;}="";
 public byte[] SigningBytes()=>JsonSerializer.SerializeToUtf8Bytes(new object[]{Action,Connection,Certificate,To,Data,Permission,Target,After,Time,Nonce});
 public void Sign(X509Certificate2 identity){Time=DateTimeOffset.UtcNow.ToUnixTimeSeconds();Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(16));using var key=identity.GetECDsaPrivateKey()??throw new CryptographicException("Missing identity key");Signature=Convert.ToBase64String(key.SignData(SigningBytes(),HashAlgorithmName.SHA256));}
 public bool Verify(X509Certificate2 identity){try{long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();if(Time<now-60||Time>now+60||Nonce.Length!=32||!Nonce.All(Uri.IsHexDigit))return false;using var key=identity.GetECDsaPublicKey();return key?.KeySize==256&&key.VerifyData(SigningBytes(),Convert.FromBase64String(Signature),HashAlgorithmName.SHA256);}catch{return false;}}
}
public static class ChatCertificates {
 public static string Fingerprint(X509Certificate2 cert)=>Convert.ToHexString(SHA256.HashData(cert.RawData));
 public static X509Certificate2 Create(){using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var created=new CertificateRequest("CN=SPT Assistant Chat",key,HashAlgorithmName.SHA256){CertificateExtensions={new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,true)}}.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(10));byte[] pfx=created.Export(X509ContentType.Pfx);try{return X509CertificateLoader.LoadPkcs12(pfx,null,X509KeyStorageFlags.DefaultKeySet|X509KeyStorageFlags.Exportable);}finally{CryptographicOperations.ZeroMemory(pfx);}}
 public static X509Certificate2 Read(string certificate)=>X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificate));
}
// A bounded reliable byte stream for TLS records over an opaque HTTP relay.
// TLS owns encryption, integrity, key exchange and authentication; this class
// only delivers ordered bytes and never interprets application plaintext.
public sealed class ChatRelayStream:Stream {
 readonly Channel<byte[]> incoming=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(128){FullMode=BoundedChannelFullMode.Wait,SingleReader=true});
 readonly Func<byte[],CancellationToken,Task> send;readonly SemaphoreSlim writing=new(1,1);byte[] current=Array.Empty<byte>();int offset;bool disposed;
 public ChatRelayStream(Func<byte[],CancellationToken,Task> send)=>this.send=send;
 public bool Feed(byte[] data){if(data.Length is 0 or >32768||!incoming.Writer.TryWrite(data)){Dispose();return false;}return true;}
 public override bool CanRead=>!disposed;public override bool CanWrite=>!disposed;public override bool CanSeek=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
 public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default){if(buffer.Length==0)return 0;while(offset==current.Length){if(!await incoming.Reader.WaitToReadAsync(ct))return 0;current=await incoming.Reader.ReadAsync(ct);offset=0;}int n=Math.Min(buffer.Length,current.Length-offset);current.AsMemory(offset,n).CopyTo(buffer);offset+=n;return n;}
 public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken ct=default){ObjectDisposedException.ThrowIf(disposed,this);await writing.WaitAsync(ct);try{for(int start=0;start<buffer.Length;start+=16384){ObjectDisposedException.ThrowIf(disposed,this);await send(buffer.Slice(start,Math.Min(16384,buffer.Length-start)).ToArray(),ct);}}finally{writing.Release();}}
 public override int Read(byte[] b,int o,int n)=>ReadAsync(b.AsMemory(o,n)).AsTask().GetAwaiter().GetResult();public override void Write(byte[] b,int o,int n)=>WriteAsync(b.AsMemory(o,n)).AsTask().GetAwaiter().GetResult();
 public override void Flush(){}public override Task FlushAsync(CancellationToken ct)=>Task.CompletedTask;public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
 protected override void Dispose(bool value){disposed=true;incoming.Writer.TryComplete();base.Dispose(value);}
}
public sealed record ChatMessage(string Kind,string Text,string Id="",string Permission="",string Payload="");
