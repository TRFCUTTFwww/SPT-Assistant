using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt.WorldEditing;
public sealed class ContainerLootOptions {
 public string Mode {get;set;}="server";
 public int Rolls {get;set;}=6;
 public bool FoundInRaid {get;set;}=true;
 public List<ContainerLootChoice> Items {get;set;}=new();
 public void Validate(){
  if(Mode is not ("server" or "custom" or "empty"))throw new ArgumentException("未知物资模式。");
  if(Rolls<0||Rolls>100||(Items==null||Items.Count>100))throw new ArgumentException("物资抽取次数及候选物品最多 100 项。");
  foreach(var item in Items)if(item==null||item.Template==null||item.Template.Length!=24||!item.Template.All(Uri.IsHexDigit)||!double.IsFinite(item.Weight)||item.Weight<=0)throw new ArgumentException("请检查候选物品 ID和正数权重。");
  if(Mode=="custom"&&Rolls>0&&Items.Count==0)throw new ArgumentException("请先添加自定义物资候选项。");
 }
}
public sealed class ContainerLootChoice {public string Template{get;set;}="";public double Weight{get;set;}=1;}
public static class ContainerBuildRules {
 public static float Yaw(float angle){if(!float.IsFinite(angle))throw new ArgumentException("旋转角度无效。");return (angle%360+360)%360;}
 public static float Snap(float value,float grid){if(!float.IsFinite(value)||!float.IsFinite(grid)||grid<0)throw new ArgumentException("网格参数无效。");return grid==0?value:(float)(Math.Round(value/grid,MidpointRounding.AwayFromZero)*grid);}
}
