using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class BuffPeerRegressionChecks {
 internal static void Run(AssemblyDefinition core,AssemblyDefinition native){
  void Check(bool value,string reason){if(!value)throw new Exception(reason);}
  MethodReference[] Calls(MethodDefinition method)=>method.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
  var effects=core.MainModule.Types.Single(t=>t.Name=="CharacterBuffs");
  var root=effects.Methods.Single(m=>m.Name=="ReadAiHud");
  var reachable=new HashSet<MethodDefinition>();
  void Walk(MethodDefinition method){if(!reachable.Add(method)||!method.HasBody)return;foreach(var call in Calls(method)){if(call.DeclaringType.Name!="CharacterBuffs")continue;var found=effects.Methods.FirstOrDefault(m=>m.FullName==call.FullName);if(found!=null)Walk(found);}}
  Walk(root);
  var readCalls=reachable.Where(m=>m.HasBody).SelectMany(Calls).ToArray();
  Check(readCalls.Any(m=>m.Name=="get_CurrentStrength")&&readCalls.Any(m=>m.Name=="get_TimeLeft")&&readCalls.Any(m=>m.Name=="get_BodyPart"),"HUD reads native current effect intensity, body part and remaining time");
  Check(readCalls.Any(m=>m.Name=="get__buffs")&&readCalls.Any(m=>m.Name=="get_Value")&&readCalls.Any(m=>m.Name=="get_WholeTime")&&readCalls.Any(m=>m.Name=="SubState"),"HUD reads individual stimulator value, body part, delay and refreshed expiration");
  Check(!readCalls.Any(m=>m.Name is "Bind" or "Scan" or "SelectAi" or "ClearSnapshot" or "Tracking" or "AddEffect" or "DoExternalBuff" or "ForceRemove" or "set_State"),"HUD traversal cannot change editor target, ownership tracking or native effects");
  Check(!reachable.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.OpCode.Code==Code.Stsfld&&i.Operand is FieldReference f&&new[]{"rows","added","aiTarget","aiTargets","worldPointer","healthPointer","profile"}.Contains(f.Name)),"HUD snapshot keeps all editor selection and removal state isolated");
  var reader=effects.Methods.Single(m=>m.Name=="ReadRows");Check(reader.Parameters.Any(p=>p.Name=="currentOnly")&&reader.Body.Instructions.Any(i=>Equals(i.Operand,"已结束"))&&reader.Body.Instructions.Any(i=>Equals(i.Operand,"未触发")),"HUD omits finished and untriggered stimulator children while retaining editor details");
  var stim=native.MainModule.GetTypes().Single(t=>t.FullName=="EFT.HealthSystem.ActiveHealthController/Stimulator/Buff");
  foreach(string getter in new[]{"get_BodyPart","get_Value","get_WholeTime","get_Active","get_Processed"})Check(stim.Methods.Any(m=>m.Name==getter),"native sub-buff getter exists: "+getter);
  var hud=core.MainModule.Types.Single(t=>t.Name=="AiBuffHud");var tick=Calls(hud.Methods.Single(m=>m.Name=="Tick"));
  Check(tick.Any(m=>m.DeclaringType.Name=="CrosshairProbe"&&m.Name=="Read")&&tick.Any(m=>m.DeclaringType.Name=="OpticCameraManager"&&m.Name=="get_Camera")&&tick.Any(m=>m.DeclaringType.Name=="FikaTeams"&&m.Name=="IsAi"),"crosshair HUD follows scope camera and rejects human Fika peers");
  var overlay=core.MainModule.Types.Single(t=>t.Name=="Overlay");
  Check(Calls(overlay.Methods.Single(m=>m.Name=="LateUpdate")).Any(m=>m.DeclaringType.Name=="AiBuffHud"&&m.Name=="Tick")&&Calls(overlay.Methods.Single(m=>m.Name=="OnGUI")).Any(m=>m.DeclaringType.Name=="AiBuffHud"&&m.Name=="Draw"),"HUD updates and draws independently from people/loot toggles");
  Check(Calls(overlay.Methods.Single(m=>m.Name=="DrawFixedMarkers")).Any(m=>m.DeclaringType.Name=="AiBuffHud"&&m.Name=="get_Visible"),"AI and container detail panels share the left screen region without overlap");
  var peer=core.MainModule.Types.Single(t=>t.Name=="PlayerTeleportTarget");var resolve=Calls(peer.Methods.Single(m=>m.Name=="Resolve"));var eligibility=Calls(peer.Methods.Single(m=>m.Name=="Eligible"));
  Check(resolve.Any(m=>m.Name=="get_AllAlivePlayersList")&&resolve.Any(m=>m.Name=="get_ProfileId")&&resolve.Any(m=>m.Name=="get_Pointer")&&resolve.Any(m=>m.Name=="get_Active")&&resolve.Any(m=>m.Name=="Eligible"),"player destination re-resolves world, live roster, pointer, profile and eligibility at execution");
  Check(eligibility.Any(m=>m.Name=="get_IsYourPlayer")&&eligibility.Any(m=>m.Name=="get_IsAlive")&&eligibility.Any(m=>m.DeclaringType.Name=="FikaTeams"&&m.Name=="IsAi"),"peer destinations reject self, dead players and replicated AI");
  var teleport=core.MainModule.Types.Single(t=>t.Name=="TeleportControl");var execute=teleport.Methods.Single(m=>m.Name=="Execute");var executeCalls=Calls(execute);
  Check(executeCalls.Any(m=>m.DeclaringType.Name=="PlayerTeleportTarget"&&m.Name=="Resolve")&&executeCalls.Any(m=>m.Name=="Landing")&&executeCalls.Any(m=>m.Name=="Move"),"peer transfer uses fresh target position and existing safe local landing/movement");
  var code=execute.Body.Instructions;int moving=code.ToList().FindIndex(i=>i.Operand is MethodReference call&&call.Name=="Moving");
  Check(code.Take(moving).Any(i=>i.OpCode.Code==Code.Stsfld&&i.Operand is FieldReference f&&f.Name=="useAiSources"),"human destination forces local mover before resolving any old AI source selection");
  Check(!peer.Methods.Where(m=>m.HasBody).SelectMany(Calls).Any(m=>m.Name is "Teleport" or "Move" or "set_Position"),"peer destination wrapper never moves remote players");
  var actor=core.MainModule.Types.Single(t=>t.Name=="LiveActorReference");Check(Calls(actor.Methods.Single(m=>m.Name=="Resolve")).Any(m=>m.Name=="get_IsAI"),"all existing AI editing references retain their original strict AI guard");
  Console.WriteLine("PASS read-only AI Buff snapshots, native sub-effect body/value/timers, scope selection, left panel arbitration and live local-to-peer teleport guards");
 }
}
