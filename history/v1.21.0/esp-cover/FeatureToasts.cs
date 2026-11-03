using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FeatureToasts {
 sealed record Toast(string Text,float Time);
 static readonly System.Collections.Concurrent.ConcurrentQueue<string> pending=new();static readonly List<Toast> entries=new();static ConfigFile? config;
 internal static void Initialize(Plugin plugin){config=plugin.Config;config.SettingChanged+=Changed;}
 static void Changed(object? sender,SettingChangedEventArgs args){var entry=args.ChangedSetting;bool enabled;if(entry.BoxedValue is bool flag)enabled=flag;else if(entry.BoxedValue is Enum mode&&Enum.GetNames(mode.GetType()).Contains("Off"))enabled=mode.ToString()!="Off";else return;string name=entry.Description.Tags.OfType<CnConfig.ConfigurationManagerAttributes>().FirstOrDefault()?.DispName??entry.Definition.Key;Show(name,enabled);}
 internal static void Show(string name,bool enabled)=>Show(name+(enabled?" · 已开启":" · 已关闭"));
 internal static void Show(string text)=>pending.Enqueue(text);
 static void Add(string text){if(entries.Count>0&&entries[^1].Text==text&&Time.unscaledTime-entries[^1].Time<.2f)return;entries.Add(new(text,Time.unscaledTime));while(entries.Count>5)entries.RemoveAt(0);}
 internal static void Draw(){while(pending.TryDequeue(out var text))Add(text);entries.RemoveAll(e=>Time.unscaledTime-e.Time>4);float width=Math.Min(390,Screen.width-32);for(int i=entries.Count-1,n=0;i>=0;i--,n++){var toast=entries[i];float alpha=Math.Clamp((4-(Time.unscaledTime-toast.Time))/.5f,0,1);var r=new Rect(Screen.width-width-18,Screen.height-66-n*49,width,41);InterfaceMenu.Panel(r,alpha);InterfaceMenu.Surface(new Rect(r.x,r.y+9,3,r.height-18),new Color(.4f,.9f,.75f,alpha),1);InterfaceMenu.Label(new Rect(r.x+14,r.y+5,r.width-26,r.height-10),toast.Text,new Color(1,1,1,alpha),13);}}
 internal static void Shutdown(){if(config!=null)config.SettingChanged-=Changed;config=null;entries.Clear();pending.Clear();}
}
