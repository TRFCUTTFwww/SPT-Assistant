using AssistantChat;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
internal static class RelayIntegration {
 internal static async Task Run(){using var alice=new Endpoint("alice");using var bob=new Endpoint("bob");using var broker=new ChatRelayBroker((_,_)=>new[]{"chat.send"});
  alice.Exchange(broker,"register");bob.Exchange(broker,"register");alice.Exchange(broker,"trust",bob.Id);bob.Exchange(broker,"trust",alice.Id);
  alice.Open(bob,broker,true);bob.Open(alice,broker,false);
  using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(15));var pump=Task.Run(async()=>{while(!cts.IsCancellationRequested){alice.Exchange(broker,"poll");bob.Exchange(broker,"poll");await Task.Delay(2,cts.Token);}},cts.Token);
  try{await Task.WhenAll(alice.Channel!.Ready,bob.Channel!.Ready).WaitAsync(cts.Token);await alice.Channel.Send(new("chat","through signed broker"));await bob.Channel.Send(new("whisper","return path"));var fromAlice=await bob.Inbox.Task.WaitAsync(cts.Token);var fromBob=await alice.Inbox.Task.WaitAsync(cts.Token);if(fromAlice.Text!="through signed broker"||fromBob.Text!="return path")throw new Exception("integration mismatch");Console.WriteLine("PASS signed relay, ack cursor and mutually authenticated encrypted session integration");}finally{cts.Cancel();try{await pump;}catch(OperationCanceledException){}}
 }
 sealed class Endpoint(string profile):IDisposable {
  public readonly string Id=Guid.NewGuid().ToString("N");public readonly X509Certificate2 Identity=ChatCertificates.Create();public readonly TaskCompletionSource<ChatMessage> Inbox=new(TaskCreationOptions.RunContinuationsAsynchronously);public ChatSecureChannel? Channel;readonly object gate=new();long cursor;
  public void Exchange(ChatRelayBroker broker,string action,string to="",byte[]? bytes=null){lock(gate){var r=new ChatRequest{Action=action,Connection=Id,Certificate=Convert.ToBase64String(Identity.RawData),After=cursor,To=to,Data=bytes==null?"":Convert.ToBase64String(bytes)};r.Sign(Identity);var response=broker.Handle(r,profile,profile); // Force the same serialization boundary as real HTTP.
   response=JsonSerializer.Deserialize<ChatPoll>(JsonSerializer.Serialize(response))!;if(response.Error.Length>0)throw new Exception(response.Error);foreach(var packet in response.Packets){if(packet.Sequence<=cursor)continue;if(packet.Sequence!=cursor+1)throw new Exception("cursor gap");if(Channel==null)throw new Exception("channel not prepared");if(!Channel.Relay.Feed(Convert.FromBase64String(packet.Data)))throw new Exception("feed failure");cursor=packet.Sequence;}}}
  public void Open(Endpoint peer,ChatRelayBroker broker,bool initiator){Channel=new(Identity,ChatCertificates.Fingerprint(peer.Identity),initiator,(bytes,_)=>{Exchange(broker,"send",peer.Id,bytes);return Task.CompletedTask;},m=>Inbox.TrySetResult(m),error=>Inbox.TrySetException(new Exception(error)),profile+Id+peer.Id,peer.Profile+peer.Id+Id);}
  string Profile=>profile;
  public void Dispose(){Channel?.Dispose();Identity.Dispose();}
 }
}
