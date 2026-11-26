using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace LocalSpt5Esp;
internal sealed class SavedTeleportPoint {
 public string Id{get;set;}="";public string Name{get;set;}="";public string Map{get;set;}="";public float X{get;set;}public float Y{get;set;}public float Z{get;set;}
 internal Vector3 Position=>new(X,Y,Z);
}
internal sealed record TeleportMapPoint(string Id,string Map,string Name,string Key,Vector3 Position,bool Popular);
internal static class TeleportRules {
 internal static bool Finite(Vector3 point)=>float.IsFinite(point.X)&&float.IsFinite(point.Y)&&float.IsFinite(point.Z);
 internal static Vector3 Coordinates(string x,string y,string z){float Parse(string raw){if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)||!float.IsFinite(value))throw new ArgumentException("X／Y／Z 须为有限数字。");return value;}return new(Parse(x),Parse(y),Parse(z));}
 internal static Vector3 FromMap(float x,float y,float z)=>new(x,z,y);
 internal static string Map(string raw)=>raw.Trim().ToLowerInvariant() switch {"factory4_night"=>"factory4_day","sandbox_high"=>"sandbox",var map=>map};
 internal static string Name(string raw){string value=raw.Trim();if(value.Length is <1 or >100)throw new ArgumentException("请输入 1～100 字的传送点名称。");return value;}
 internal static bool Popular(string name){string text=name.ToLowerInvariant();return new[]{"marked","kiba","ultra medical","chekannaya","chek 15","relax","rb-bk","rb-vo","rb-pkpm","red keycard","blue keycard","green keycard","black keycard","violet keycard","yellow keycard","sanitar","tape key","health resort 301","health resort 226","health resort 222","health resort 110","health resort 216","west wing room 301","west wing room 216","west wing room 218","east wing room 222","east wing room 226","infirmary (blue)","sterile lab (green)","test room (black)","security office (red)","security office (violet)","security #2 (yellow)","arsenal storage"}.Any(text.Contains);}
 internal static List<TeleportMapPoint> ReadMapDirectory(string root,List<string> errors){var result=new List<TeleportMapPoint>();if(!Directory.Exists(root))return result;foreach(string file in Directory.EnumerateFiles(root,"*.jsonc",SearchOption.AllDirectories))try{using var document=JsonDocument.Parse(File.ReadAllText(file),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});var data=document.RootElement;if(!data.TryGetProperty("MapInternalNames",out var maps)||!data.TryGetProperty("StaticMarkers",out var markers))continue;int ordinal=0;foreach(var marker in markers.EnumerateArray()){ordinal++;if(!marker.TryGetProperty("Category",out var category)||category.GetString()!="LockedDoor"||!marker.TryGetProperty("Position",out var position))continue;string name=marker.TryGetProperty("Text",out var text)?text.GetString()??"钥匙房":"钥匙房",key=marker.TryGetProperty("AssociatedItemId",out var item)?item.GetString()??"":"";var point=FromMap(position.GetProperty("x").GetSingle(),position.GetProperty("y").GetSingle(),position.GetProperty("z").GetSingle());if(!Finite(point))continue;foreach(var map in maps.EnumerateArray()){string id=Map(map.GetString()??"");result.Add(new(Path.GetFileNameWithoutExtension(file)+":"+ordinal,id,name,key,point,Popular(name)));}}}catch(Exception error){errors.Add(Path.GetFileName(file)+": "+error.Message);}return result;}
 internal static string MapName(string id)=>Map(id) switch {"bigmap"=>"海关","factory4_day"=>"工厂","sandbox"=>"中心区","interchange"=>"立交桥","laboratory"=>"实验室","lighthouse"=>"灯塔","rezervbase"=>"储备站","shoreline"=>"海岸线","tarkovstreets"=>"塔科夫街区","woods"=>"森林","labyrinth"=>"迷宫",var raw=>raw};
 internal static void Validate(SavedTeleportPoint point){if(string.IsNullOrWhiteSpace(point.Id)||string.IsNullOrWhiteSpace(point.Map)||string.IsNullOrWhiteSpace(point.Name)||!Finite(point.Position))throw new ArgumentException("传送点内容无效。");}
 internal static bool SameMap(string a,string b)=>Map(a)==Map(b);
}
