using System;
namespace LocalSpt5Esp;
internal static partial class RaidCommands {
 static bool AssistantCommand(string name,string[] args){
  if(name=="misc"){MiscPanel.Command(args);return true;}
  if(name!="fir")return false;
  if(args.Length!=2||args[1] is not ("on" or "off" or "add" or "remove"))throw new ArgumentException("/fir <物品实例ID> on|off");
  if(ItemSpawner.InRaid())MultiplayerGuard(World(),name,args);
  ItemFirContext.Command(args[0],args[1] is "on" or "add");return true;
 }
}
