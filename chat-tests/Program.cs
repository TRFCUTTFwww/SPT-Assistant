using AssistantChat;
using LocalSpt5Esp;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Text.Json;
if(args.Length==2&&args[0]=="--http"){await HttpRelayIntegration.Run(args[1]);return;}
static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
using var a=ChatCertificates.Create();using var b=ChatCertificates.Create();
bool revoked=false;using var broker=new ChatRelayBroker((profile,_)=>revoked&&profile=="b"?Array.Empty<string>():new[]{"chat.send"});
string aid=Guid.NewGuid().ToString("N"),bid=Guid.NewGuid().ToString("N");
ChatRequest Req(string action,bool alice=true,string to="",string data="",long after=0){var cert=alice?a:b;var r=new ChatRequest{Action=action,Connection=alice?aid:bid,Certificate=Convert.ToBase64String(cert.RawData),To=to,Data=data,After=after};r.Sign(cert);return r;}
Check(broker.Handle(Req("register"),"a","Alice").Error==""&&broker.Handle(Req("register",false),"b","Bob").Error=="","relay registers signed profile-bound identities");
var replay=Req("poll");Check(broker.Handle(replay,"a","Alice").Error==""&&broker.Handle(replay,"a","Alice").Error.Contains("Replay"),"relay rejects nonce replay");
Check(broker.Handle(Req("poll"),"b","Bob").Error!="","profile cannot use another connection");
Check(broker.Handle(Req("send",to:bid,data:"AQ=="),"a","Alice").Error!="","unverified recipients cannot receive TLS bytes");
Check(broker.Handle(Req("trust",to:bid),"a","Alice").Error=="","first peer declares fingerprint verification");
Check(broker.Handle(Req("send",to:bid,data:"AQ=="),"a","Alice").Error!="","one-sided verification cannot start handshake");
Check(broker.Handle(Req("trust",false,aid),"b","Bob").Error=="","mutual verification registered");
Check(broker.Handle(Req("send",to:bid,data:"AQID"),"a","Alice").Error=="","opaque record accepted");
var poll=broker.Handle(Req("poll",false),"b","Bob");Check(poll.Packets.Length==1&&poll.Packets[0].Data=="AQID","relay cursor returns pending bytes");
Check(broker.Handle(Req("poll",false,after:poll.Packets[0].Sequence),"b","Bob").Packets.Length==0,"acknowledged bytes removed");
Check(broker.Handle(Req("poll",false,after:99),"b","Bob").Error!="","future acknowledgement rejected");
revoked=true;Check(broker.Handle(Req("send",to:bid,data:"AQ=="),"a","Alice").Error!="","recipient permission revoked immediately");revoked=false;
Check(broker.Handle(Req("send",to:bid,data:Convert.ToBase64String(new byte[32769])),"a","Alice").Error!="","oversized records rejected");
Check(ChatCommandSyntax.CompletionStart("/tp \"boss:Big P")==4,"quoted-name completion replaces entire token");
var request=new ChatRequest{Connection=Guid.NewGuid().ToString("N"),Action="poll",Certificate=Convert.ToBase64String(a.RawData)};request.Sign(a);Check(request.Verify(a),"signed relay request");Check(!request.Verify(b),"wrong signing identity rejected");request.To="tampered";Check(!request.Verify(a),"modified destination rejected");request.Sign(a);request.Time=long.MinValue;Check(!request.Verify(a),"invalid timestamp cannot overflow validation");
Check(ChatCommandSyntax.Tokens("tp \"boss:Big Pipe\" ~ ~1 ~").SequenceEqual(new[]{"tp","boss:Big Pipe","~","~1","~"}),"quoted names and coordinates");Check(ChatCommandSyntax.Tokens("kill @e[type=boss,name=Big Pipe,limit=1]").Length==2,"selector spaces stay in one token");Check(ChatCommandSyntax.Tokens(ChatCommandSyntax.Quote("quote \" slash \\ name"))[0]=="quote \" slash \\ name","completion quoting roundtrip");bool bad=false;try{ChatCommandSyntax.Tokens("tp \"open");}catch(ArgumentException){bad=true;}Check(bad,"unclosed quote rejected");Check(ChatCommandSyntax.Duration("15m")==900&&ChatCommandSyntax.Duration("01:02:03")==3723,"duration units and hms");
var inboxA=new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);var inboxB=new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);var errors=new ConcurrentQueue<string>();ChatSecureChannel? ca=null,cb=null;var waiting=new ConcurrentQueue<byte[]>();var wire=new ConcurrentQueue<byte[]>();
ca=new(a,ChatCertificates.Fingerprint(b),true,(bytes,ct)=>{wire.Enqueue(bytes);if(cb==null)waiting.Enqueue(bytes);else cb.Relay.Feed(bytes);return Task.CompletedTask;},m=>inboxA.TrySetResult(m),errors.Enqueue);
cb=new(b,ChatCertificates.Fingerprint(a),false,(bytes,ct)=>{wire.Enqueue(bytes);ca.Relay.Feed(bytes);return Task.CompletedTask;},m=>inboxB.TrySetResult(m),errors.Enqueue);
while(waiting.TryDequeue(out var chunk))cb.Relay.Feed(chunk);
using(ca)using(cb){try{await Task.WhenAll(ca.Ready,cb.Ready).WaitAsync(TimeSpan.FromSeconds(15));}catch{Console.WriteLine("TLS diagnostic: A="+ca.Ready.Status+" "+ca.Ready.Exception+" B="+cb.Ready.Status+" "+cb.Ready.Exception+" wire="+string.Join(",",wire.Select(b=>b.Length))+" errors="+string.Join("|",errors));throw;}Check(true,"mutually authenticated fresh TLS 1.3");await ca.Send(new("chat","秘密消息 /kill is only text"));await cb.Send(new("whisper","private response"));Check((await inboxB.Task.WaitAsync(TimeSpan.FromSeconds(5))).Text=="秘密消息 /kill is only text","encrypted unicode message roundtrip");Check((await inboxA.Task.WaitAsync(TimeSpan.FromSeconds(5))).Kind=="whisper","bidirectional encrypted frame");byte[] plaintext=System.Text.Encoding.UTF8.GetBytes("秘密消息");Check(!wire.Any(record=>record.AsSpan().IndexOf(plaintext)>=0),"relay records do not contain plaintext");}
Check(errors.IsEmpty,"healthy session has no errors");
var firstRecords=wire.First();var secondWire=new ConcurrentQueue<byte[]>();ChatSecureChannel? x=null,y=null;var pending=new ConcurrentQueue<byte[]>();x=new(a,ChatCertificates.Fingerprint(b),true,(bytes,ct)=>{secondWire.Enqueue(bytes);if(y==null)pending.Enqueue(bytes);else y.Relay.Feed(bytes);return Task.CompletedTask;},_=>{},_=>{});y=new(b,ChatCertificates.Fingerprint(a),false,(bytes,ct)=>{x.Relay.Feed(bytes);return Task.CompletedTask;},_=>{},_=>{});while(pending.TryDequeue(out var c))y.Relay.Feed(c);using(x)using(y){await Task.WhenAll(x.Ready,y.Ready).WaitAsync(TimeSpan.FromSeconds(15));Check(!firstRecords.SequenceEqual(secondWire.First()),"reconnect produces fresh handshake records");}
ChatSecureChannel? wrongA=null,wrongB=null;var wrongPending=new ConcurrentQueue<byte[]>();wrongA=new(a,new string('0',64),true,(bytes,ct)=>{if(wrongB==null)wrongPending.Enqueue(bytes);else wrongB.Relay.Feed(bytes);return Task.CompletedTask;},_=>{},_=>{});wrongB=new(b,ChatCertificates.Fingerprint(a),false,(bytes,ct)=>{wrongA.Relay.Feed(bytes);return Task.CompletedTask;},_=>{},_=>{});while(wrongPending.TryDequeue(out var c))wrongB.Relay.Feed(c);using(wrongA)using(wrongB){bad=false;try{await wrongA.Ready.WaitAsync(TimeSpan.FromSeconds(15));}catch(Exception e) when(e is AuthenticationException or System.IO.IOException or OperationCanceledException){bad=true;}Check(bad,"unverified peer certificate fails closed");}
using(var stream=new ChatRelayStream((_,_)=>Task.CompletedTask)){for(int i=0;i<128;i++)CheckFeed(stream.Feed(new byte[]{1}));Check(!stream.Feed(new byte[]{2}),"bounded input queue closes on overflow");}
static void CheckFeed(bool result){if(!result)throw new Exception("unexpected queue failure");}
foreach(var sample in new[]{("summon",new[]{"assault","~"}),("time",new[]{"query","typo"}),("kill",new[]{"@e[type=ai]","ignored"}),("marker",new[]{"remove","x","extra"}),("tp",new[]{"@s","~","~","~","facing"})}){bad=false;try{ChatCommandSyntax.ValidateArguments(sample.Item1,sample.Item2);}catch(ArgumentException){bad=true;}Check(bad,"reject malformed arguments before mutation: "+sample.Item1);}
ChatCommandSyntax.ValidateArguments("tp",new[]{"@s","~","~","~","facing","~","~1","~"});ChatCommandSyntax.ValidateArguments("summon",new[]{"assault","~","~","~","2","1"});Check(true,"valid teleport and summon optional arguments");
bad=false;try{ChatCommandSyntax.Duration("");}catch(ArgumentException){bad=true;}Check(bad,"empty duration rejected explicitly");
using(var invalidPolicy=new ChatRelayBroker((_,_)=>throw new UnauthorizedAccessException()))Check(invalidPolicy.Handle(Req("register"),"a","Alice").Error.Length>0,"unreadable relay policy fails closed");
ChatSecureChannel? boundA=null,boundB=null;var boundPending=new ConcurrentQueue<byte[]>();boundA=new(a,ChatCertificates.Fingerprint(b),true,(bytes,_)=>{if(boundB==null)boundPending.Enqueue(bytes);else boundB.Relay.Feed(bytes);return Task.CompletedTask;},_=>{},_=>{},"alice","wrong-bob");boundB=new(b,ChatCertificates.Fingerprint(a),false,(bytes,_)=>{boundA.Relay.Feed(bytes);return Task.CompletedTask;},_=>{},_=>{},"bob","alice");while(boundPending.TryDequeue(out var chunk))boundB.Relay.Feed(chunk);using(boundA)using(boundB){bad=false;try{await boundA.Ready.WaitAsync(TimeSpan.FromSeconds(15));}catch(AuthenticationException){bad=true;}Check(bad,"TLS rejects mismatched encrypted profile/connection binding");}
string policyPin=ChatCertificates.Fingerprint(a);string Policy(string admins,string blocked)=>"{\"admins\":"+admins+",\"blocked\":"+blocked+"}";string entry=JsonSerializer.Serialize(new[]{new{profile="a",fingerprint=policyPin.ToLowerInvariant()}});
Check(ChatRelayPolicy.Read(Policy(entry,"[]"),"a",policyPin).Contains("admin"),"policy binds admin to profile and certificate");
Check(!ChatRelayPolicy.Read(Policy(entry,"[]"),"other",policyPin).Contains("admin"),"policy rejects other profile with same key");
Check(ChatRelayPolicy.Read(Policy(entry,entry),"a",policyPin).Length==0,"policy block overrides admin");
foreach(string malformed in new[]{"{}",Policy("null","[]"),Policy("[]","[{}]")}){bad=false;try{ChatRelayPolicy.Read(malformed,"a",policyPin);}catch(JsonException){bad=true;}Check(bad,"malformed policy fails closed");}
await RelayIntegration.Run();
Console.WriteLine("All chat transport and syntax tests passed.");
