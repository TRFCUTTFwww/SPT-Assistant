using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FeatureToasts {
 sealed record Request(string Text,float Lifetime,IntPtr Raid);
 sealed record Toast(Request Notice,float Time);
 static readonly System.Collections.Concurrent.ConcurrentQueue<Request> pending=new();static readonly List<Toast> entries=new();static ConfigFile? config;
 internal static void Initialize(Plugin plugin){config=plugin.Config;config.SettingChanged+=Changed;}
 static void Changed(object? sender,SettingChangedEventArgs args){var entry=args.ChangedSetting;bool enabled;if(entry.BoxedValue is bool flag)enabled=flag;else if(entry.BoxedValue is Enum mode&&Enum.GetNames(mode.GetType()).Contains("Off"))enabled=mode.ToString()!="Off";else return;string name=entry.Description.Tags.OfType<CnConfig.ConfigurationManagerAttributes>().FirstOrDefault()?.DispName??entry.Definition.Key;Show(name,enabled);}
 internal static void Show(string name,bool enabled)=>Show(name+(enabled?" · 已开启":" · 已关闭"));
 internal static void Show(string text)=>pending.Enqueue(new(text,4,IntPtr.Zero));
 internal static void ShowRaidEvent(string text,float duration,IntPtr raid)=>pending.Enqueue(new(text,duration,raid));
 static void Add(Request notice){if(entries.Count>0&&entries[^1].Notice==notice&&Time.unscaledTime-entries[^1].Time<.2f)return;entries.Add(new(notice,Time.unscaledTime));while(entries.Count>5)entries.RemoveAt(0);}
 internal static void Draw(){
  if(!DirectorStage.ModNotifications||DirectorStage.HudHidden){pending.Clear();entries.Clear();return;}
  var raid=RaidNotifications.VisibleRaid;
  bool Visible(Request item)=>item.Raid==IntPtr.Zero||item.Raid==raid;
  while(pending.TryDequeue(out var notice))if(Visible(notice))Add(notice);
  entries.RemoveAll(e=>!Visible(e.Notice)||Time.unscaledTime-e.Time>=e.Notice.Lifetime);
  float width=Math.Min(I18n.English?480:390,Screen.width-32),bottom=Screen.height-25;
  for(int i=entries.Count-1;i>=0;i--){var toast=entries[i];string message=I18n.Text(toast.Notice.Text);float height=Math.Clamp(InterfaceMenu.ParagraphHeight(message,width-28,13)+20,41,Math.Max(41,Screen.height/3));
   float alpha=Math.Clamp((toast.Notice.Lifetime-(Time.unscaledTime-toast.Time))/.5f,0,1);var r=new Rect(Screen.width-width-18,bottom-height,width,height);bottom=r.y-8;
   InterfaceMenu.Panel(r,alpha);InterfaceMenu.Surface(new Rect(r.x,r.y+9,3,r.height-18),new Color(.4f,.9f,.75f,alpha),1);
   InterfaceMenu.Paragraph(new Rect(r.x+14,r.y+9,r.width-28,r.height-18),message,new Color(1,1,1,alpha),13);
  }
 }
 internal static void Shutdown(){if(config!=null)config.SettingChanged-=Changed;config=null;entries.Clear();pending.Clear();}
}
