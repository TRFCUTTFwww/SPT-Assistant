using AssistantChat;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public class ChatRelayRequest:ChatRequest,IRequestData {}
[Injectable]
public class ChatRelayRouter:StaticRouter {
 public ChatRelayRouter(JsonUtil json,ChatRelayService relay):base(json,[new RouteAction<ChatRelayRequest>("/local/assistant/chat",(_,r,s,_,_)=>new ValueTask<string>(relay.Handle(r,s)))]){}
}
// Only opaque TLS records pass through this service. Profile names come from
// the authenticated SPT session. No command execution or plaintext logging.
[Injectable]
public class ChatRelayService {
 readonly SaveServer saves;readonly HttpResponseUtil http;readonly ChatRelayBroker broker;
 readonly string policyPath=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","chat-permissions.json");
 public ChatRelayService(SaveServer saves,HttpResponseUtil http){this.saves=saves;this.http=http;broker=new(Permissions);}
 public string Handle(ChatRelayRequest r,MongoId session){var info=saves.GetProfile(session)?.CharacterData?.PmcData?.Info;return http.GetBody(info==null?new ChatPoll(r?.Connection??"",[],[],[],"Unknown profile"):broker.Handle(r,session.ToString(),info.Nickname??session.ToString()));}
 string[] Permissions(string profile,string fingerprint){
  if(!File.Exists(policyPath)){Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);File.WriteAllText(policyPath,"{\n  \"admins\": [],\n  \"blocked\": []\n}\n");}
  return ChatRelayPolicy.Read(File.ReadAllText(policyPath),profile,fingerprint);
 }
}
