using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class NightVisionAssist {
 static Vector2 settingsScroll;
 sealed class Channel {
  internal readonly ConfigEntry<bool> Enabled,BlockLights;
  internal readonly ConfigEntry<NightVisionStyle> Style;
  readonly ConfigEntry<string> color;
  readonly ConfigEntry<float> intensity,noise,scale,ambient,size,gain,contrast;
  readonly ConfigEntry<bool> clarity,preserveColor;
  readonly ConfigEntry<NightVisionMask> mask;
  internal readonly bool Optic;
  internal Channel(bool optic,ConfigEntry<bool>? enabled=null){
   Optic=optic;string key=optic?"Optic ":"Global ";const string section="75. 夜视效果";
   Enabled=enabled??FeatureConfig.Bind(section,key+"enabled","开镜夜视",false,"只在光学瞄准镜开镜时启用镜内夜视，收镜后恢复。");
   BlockLights=FeatureConfig.Bind(section,key+"suppress lights",optic?"镜内夜视屏蔽场景光源":"全屏夜视屏蔽场景光源",false,"屏蔽对应画面中的场景灯光、AI 与玩家枪灯、体积光和镜头光晕。全屏与镜内分别控制；保留烘焙光照和发光材质。");
   Style=FeatureConfig.Bind(section,key+"profile",optic?"镜内夜视效果":"全屏夜视效果",optic?NightVisionStyle.GPNVG18:NightVisionStyle.Current);
   var defaults=NightVisionRules.Device(NightVisionStyle.GPNVG18);
   color=FeatureConfig.Bind(section,key+"custom color","夜视颜色（HEX）",defaults.Hex);
   intensity=FeatureConfig.Bind(section,key+"custom intensity","夜视增亮强度",defaults.Intensity);
   noise=FeatureConfig.Bind(section,key+"custom noise","夜视噪点强度",defaults.NoiseIntensity);
   scale=FeatureConfig.Bind(section,key+"custom noise scale","夜视噪点比例",defaults.NoiseScale);
   ambient=FeatureConfig.Bind(section,key+"custom ambient","夜视暗部增光",defaults.Ambient);
   mask=FeatureConfig.Bind(section,key+"custom mask","夜视视野遮罩",defaults.Mask);
   size=FeatureConfig.Bind(section,key+"custom mask size","夜视遮罩大小",defaults.MaskSize);
   clarity=FeatureConfig.Bind(section,key+"custom clarity","使用 Nw11 渲染层",false,"无夜视噪点与遮罩；暗部增光仅在对应相机渲染期间补光。原生夜视强度和噪点参数只对原生渲染生效。");
   preserveColor=FeatureConfig.Bind(section,key+"custom preserve color","Nw11 保留彩色",false,"开启后保留场景颜色；关闭后使用所设 HEX 颜色呈现单色夜视。");
   gain=FeatureConfig.Bind(section,key+"custom gain","Nw11 暗部增益",12f,"增强暗部并压缩高光，预设绿／蓝为 12，Nw11昼为 10。");
   contrast=FeatureConfig.Bind(section,key+"custom contrast","Nw11 对比度",1.24f,"围绕曝光后的中间调增强对比，1 为中性。数值无人工范围限制。");
  }
  internal ConfigEntryBase[] Controls=>Optic?[Enabled,Style,BlockLights]:[Enabled,ToggleKey,Style,BlockLights];
  internal ConfigEntryBase[] Custom=>clarity.Value?[clarity,preserveColor,color,gain,contrast,ambient]:[clarity,color,intensity,noise,scale,ambient,mask,size];
  internal NightVisionProfile? Read()=>Style.Value switch{NightVisionStyle.Current=>null,NightVisionStyle.Custom=>NightVisionRules.Custom(color.Value,intensity.Value,noise.Value,scale.Value,ambient.Value,mask.Value,size.Value,clarity.Value,preserveColor.Value,gain.Value,contrast.Value),_=>NightVisionRules.Device(Style.Value)};
  internal void Copy(){
   var profile=Read()??(Optic?scopeLease:fullLease)?.Original()??NightVisionRules.Device(NightVisionStyle.GPNVG18);
   color.Value=profile.Hex;intensity.Value=profile.Intensity;noise.Value=profile.NoiseIntensity;scale.Value=profile.NoiseScale;ambient.Value=profile.Ambient;mask.Value=profile.Mask;size.Value=profile.MaskSize;clarity.Value=profile.Clarity;preserveColor.Value=profile.PreserveColor;gain.Value=profile.Gain;contrast.Value=profile.Contrast;Style.Value=NightVisionStyle.Custom;
  }
 }
 static float Height(Channel c,int columns)=>34+MathF.Ceiling(c.Controls.Length/(float)columns)*94+38+(c.Style.Value==NightVisionStyle.Custom?MathF.Ceiling(c.Custom.Length/(float)columns)*94:0)+58;
 internal static float SettingsHeight(float width){int columns=width>=560?2:1;return Height(full,columns)+Height(scope,columns)+((notice.Length>0||Nw11Vision.Notice.Length>0)?50:0);}
 internal static void DrawPage(Rect rect){
  float width=Math.Max(1,rect.width-28),height=SettingsHeight(width);
  settingsScroll=GUI.BeginScrollView(rect,settingsScroll,new(0,0,Math.Max(1,rect.width-20),Math.Max(height,rect.height-1)));
  try{DrawSettings(new(0,0,width,height));}finally{GUI.EndScrollView();}
 }
 internal static void DrawSettings(Rect rect){
  int columns=rect.width>=560?2:1;float cell=(rect.width-(columns-1)*12)/columns,y=rect.y;
  foreach(var channel in new[]{full,scope}){
   InterfaceMenu.Label(new(rect.x,y,rect.width,26),channel.Optic?"光学瞄准镜夜视":"全屏夜视",InterfaceMenu.Accent,16);y+=34;
   void Entries(ConfigEntryBase[] entries){for(int i=0;i<entries.Length;i++)InterfaceMenu.DrawEntry(entries[i],new(rect.x+i%columns*(cell+12),y+i/columns*94,cell,86));y+=MathF.Ceiling(entries.Length/(float)columns)*94;}
   Entries(channel.Controls);
   if(InterfaceMenu.Button(new(rect.x,y,rect.width,30),"以所选效果开始自定义"))try{channel.Copy();notice="";}catch(Exception e){notice=e.Message;}
   y+=38;if(channel.Style.Value==NightVisionStyle.Custom)Entries(channel.Custom);
   InterfaceMenu.Paragraph(new(rect.x+4,y,rect.width-8,52),channel.Optic?"镜内独立支持 Nw11 绿／蓝、Nw11昼和六款原生效果。仅对正在使用的光学镜片生效；机瞄、红点与自由镜头不启用。":"Nw11 绿／蓝提供无噪点高对比度夜视，Nw11昼保留彩色并增强暗部，战局时间保持不变；另有六款原生设备效果。关闭后恢复原设置。",null,12);y+=58;
  }
  string message=notice.Length>0?notice:Nw11Vision.Notice;
  if(message.Length>0)InterfaceMenu.Paragraph(new(rect.x+4,y,rect.width-8,48),message,InterfaceMenu.Accent,12);
 }
}
