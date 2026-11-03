using System;
namespace LocalRaidShared;
public sealed class RaidSettings {
 public int Schema {get;set;}=1;
 public bool LootEnabled {get;set;}
 public double LooseMultiplier {get;set;}=1;
 public double ContainerMultiplier {get;set;}=1;
 public double HighValueMultiplier {get;set;}=1;
 public double ValueThreshold {get;set;}=100000;
 public bool AirdropEnabled {get;set;}
 public string AirdropType {get;set;}="random";
 public int AirdropQuality {get;set;}
 public void Validate(){if(Schema!=1)throw new ArgumentException("设置版本不受支持。");void Range(double v,double a,double b,string n){if(!double.IsFinite(v)||v<a||v>b)throw new ArgumentException(n+"需为 "+a+"～"+b);}
  Range(LooseMultiplier,0,5,"散落物倍率");Range(ContainerMultiplier,0,5,"容器物资倍率");Range(HighValueMultiplier,.1,20,"高价值权重倍率");Range(ValueThreshold,1000,100000000,"高价值门槛");if(Array.IndexOf(new[]{"random","mixed","weaponArmor","foodMedical","barter"},AirdropType)<0||AirdropQuality<0||AirdropQuality>2)throw new ArgumentException("空投类型或品质无效。");}
 public double Weight(double original,double price)=>price>=ValueThreshold?original*HighValueMultiplier:original;
 public int AirdropWeight=>AirdropQuality==0?1:AirdropQuality==1?3:8;
}
