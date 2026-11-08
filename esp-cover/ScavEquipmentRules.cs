using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal static class ScavEquipmentRules {
 internal static bool Matches(IReadOnlyDictionary<string,string> expected,IReadOnlyDictionary<string,string> actual)=>expected.Count==actual.Count&&expected.All(row=>actual.TryGetValue(row.Key,out var item)&&item==row.Value);
 internal static bool CanSync(string requestedOwner,string currentOwner,long requestedPet,long currentPet,bool inRaid,bool matching)=>requestedOwner.Length>0&&requestedOwner==currentOwner&&requestedPet!=0&&requestedPet==currentPet&&!inRaid&&!matching;
 internal static float ContentHeight(float height,bool picker)=>Math.Max(height,picker?320:280);
 internal static (int Columns,float Header,float Footer,float ViewHeight) Layout(float width,float height){
  const float header=70,footer=94;return(width>=600?2:1,header,footer,Math.Max(0,height-header-footer-12));
 }
}
