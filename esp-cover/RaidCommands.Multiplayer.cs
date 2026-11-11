using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using EFT;
namespace LocalSpt5Esp;

// Optional reflection only: single-player installations never need a Fika assembly.
internal static class ChatFikaSession {
 static Type? backend;
 internal static int Role {
  get {
   try {
    backend??=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Fika.Core.Main.Utils.FikaBackendUtils",false)).FirstOrDefault(t=>t!=null);
    if(backend==null)return 0;
    return Convert.ToInt32(backend.GetProperty("ClientType",BindingFlags.Public|BindingFlags.Static)?.GetValue(null)??-1);
   }catch{return -1;}
  }
 }
 internal static string Description=>Role switch {2=>"FIKA 房主：可操作自己和本机控制的 AI。",1=>"FIKA 成员：可查询和操作自己的角色；AI 操作请由房主执行。",0=>"单人：本地命令可用。",_=>"FIKA 角色未能识别；仅开放查询和本地显示。"};
}
internal static partial class RaidCommands {
 internal static string CommandPermissions=>ChatFikaSession.Description+"\n不接受远端命令。FIKA 库存写入、删除/改名和全局计时尚未接入同步。";
 static void MultiplayerGuard(GameWorld w,string command,string[] args,JsonElement? options=null) {
  int role=ChatFikaSession.Role;
  bool peers=All(w).Any(p=>!p.IsYourPlayer&&!FikaTeams.IsAi(p));
  if(role==0&&!peers)return;
  if(command=="ai"&&options is {} o&&o.TryGetProperty("name",out _))throw new ArgumentException("FIKA 战局暂不支持重命名已有 AI；name 不会同步到队友。其他配置请移除 name 后执行。");
  var scope=ChatCommandAuthority.Scope(command,args);
  if(scope==ChatCommandScope.Unsupported)throw new ArgumentException("/"+command+" 的联机同步尚未适配，当前 FIKA 战局不能执行此写入。可用 /permission 查看范围。");
  if(scope==ChatCommandScope.Host&&role!=2)throw new ArgumentException("/"+command+" 必须在 FIKA 战局房主端执行（运行 SPT 后端不等于战局房主）。");
  if(scope!=ChatCommandScope.OwnedActors)return;
  if(role is not (1 or 2))throw new ArgumentException("无法识别战局控制端，当前仅允许查询和本地显示。");
  string selector=command switch {
   "tp" or "teleport"=>ChatCommandAuthority.TeleportSource(args) is var at&&at>=0?args[at]:"@s",
   "spreadplayers"=>args[4],"effect"=>args[1],_=>args.Length==0?"@s":args[0]
  };
  // Check the entire selection before the first mutation, including @a / @e.
  if(Resolve(w,selector).Any(p=>!ChatCommandAuthority.CanMutateActor(role==2,p.IsYourPlayer,FikaTeams.IsAi(p),p.AIData?.BotOwner!=null)))
   throw new ArgumentException("目标包含远程队友或非本机控制的 AI，未执行操作。队友请在自己客户端使用 @s；成员操作 AI 请交给战局房主。");
 }
}
