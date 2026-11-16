using HarmonyLib;
using SPTarkov.Common.Logger;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
namespace LocalGearMail;
[Injectable(TypePriority=OnLoadOrder.Preload-50)]
public sealed class QuietChatLogging:IOnLoad {
 public Task OnLoadAsync(CancellationToken ct){var h=new Harmony("local.assistant.chat-log-filter");h.UnpatchSelf();h.Patch(AccessTools.Method(typeof(SPTLoggerDispatcher),"Log"),prefix:new HarmonyMethod(typeof(QuietChatLogging),nameof(BeforeLog)));return Task.CompletedTask;}
 static readonly System.Reflection.PropertyInfo Level=typeof(SptLogMessage).GetProperty("LogLevel")!;
 public static bool Keep(string message,int level,bool exception){if(level>=3||exception)return true;const string route="/local/assistant/chat";int start=message.IndexOf(route,StringComparison.OrdinalIgnoreCase);if(start<0)return true;int end=start+route.Length;return end<message.Length&&!char.IsWhiteSpace(message[end])&&"?\"'[]()".IndexOf(message[end])<0;}
 static bool BeforeLog(SptLogMessage __0){if(__0.Message?.Contains("/local/assistant/chat",StringComparison.OrdinalIgnoreCase)!=true)return true;return Keep(__0.Message,Convert.ToInt32(Level.GetValue(__0)),__0.Exception!=null);}
}
