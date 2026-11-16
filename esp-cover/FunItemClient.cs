using System;
using System.Text.Json;
using System.Threading.Tasks;
using SPTushonka.Common.Http;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FunItemClient {
 internal static bool Enabled {get;private set;}
 internal static bool Known {get;private set;}
 static Task<string>? pending;static float next;
 internal static void Tick(){
  if(pending?.IsCompleted==true){try{using var doc=JsonDocument.Parse(pending.GetAwaiter().GetResult());var data=CharacterDataPanel.Data(doc.RootElement);if(data.TryGetProperty("funItemsEnabled",out var enabled)){Enabled=enabled.GetBoolean();Known=true;}}catch{}pending=null;}
  if(pending==null&&Time.unscaledTime>=next){next=Time.unscaledTime+10;pending=RequestHandler.PostJsonAsync("/local/projecte/runtime","{}");}
 }
 internal static void Reset(){pending=null;next=0;Enabled=false;Known=false;}
}
