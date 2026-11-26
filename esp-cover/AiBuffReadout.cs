using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EFT;
namespace LocalSpt5Esp;
internal static partial class CharacterBuffs {
 // Independent snapshot: looking at an AI must not rebind the editor selection,
 // effect tracking or manual removal ownership.
 internal static string[] ReadAiHud(Player actor){
  var health=actor.ActiveHealthController;if(health==null)return new[]{I18n.Text("AI 状态尚未就绪")};
  var lines=new List<string>();
  foreach(var row in ReadRows(Effects(health),true).Where(row=>BuffRules.CanRemove(row.Type))){
   lines.Add(I18n.Text(row.Name)+" · "+I18n.Text(row.Part));
   string amount=float.IsFinite(row.Strength)?row.Strength.ToString("0.###",CultureInfo.InvariantCulture):I18n.Text("未知");
   lines.Add(I18n.Text("强度")+" "+amount+" · "+I18n.Text("剩余")+" "+I18n.Text(row.Remaining)+" · "+I18n.Text(row.State));
   foreach(var detail in row.Details)lines.Add("  "+I18n.Text(detail));
  }
  return lines.Count==0?new[]{I18n.Text("当前无 Buff 或异常状态")}:lines.ToArray();
 }
}
