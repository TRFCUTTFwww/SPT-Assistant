using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EFT;
using SPTushonka.Common.Http;
using UnityEngine;
namespace LocalSpt5Esp;

internal static class CharacterDataPanel {
 static Task<string>? request;static JsonElement data;static bool requested,ready;
 static string status="",search="",wallet="";static Vector2 scroll;
 static readonly Dictionary<string,string> levels=new();
 static readonly Dictionary<string,string> names=new();
 internal static async Task<string> Call(string route,string capabilityName,Dictionary<string,object?> payload){
  try{
   using var capability=JsonDocument.Parse(await RequestHandler.PostJsonAsync("/local/gear/status","{}"));
   var info=Data(capability.RootElement);
   string required=payload.TryGetValue("action",out var action)&&action is string value&&value=="reload"?"adminHotReload":capabilityName;
   if(!info.TryGetProperty(required,out var supported)||supported.ValueKind!=JsonValueKind.True)return "{\"err\":228,\"errmsg\":\"请更新并重启服务端。\"}";
   payload["apiKey"]=File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","LocalGearMail","api-key.txt")).Trim();
   return await RequestHandler.PostJsonAsync(route,JsonSerializer.Serialize(payload));
  }catch{return "{\"err\":228,\"errmsg\":\"连接失败，请检查本地服务端。\"}";}
 }
 internal static JsonElement Data(JsonElement root)=>root.TryGetProperty("data",out var value)&&value.ValueKind==JsonValueKind.Object?value:root;
 static void Begin(Dictionary<string,object?> payload){if(request!=null)return;request=Call("/local/gear/data","characterData",payload);status="处理中…";}
 internal static string Name(string key,string fallback){if(names.TryGetValue(key,out var saved))return saved;var value=LocalizationExtensions.Localized(key,EStringCase.None);return names[key]=value==key||string.IsNullOrWhiteSpace(value)?fallback:value;}
 internal static void Draw(Rect rect,string page){
  if(ItemSpawner.InRaid()){InterfaceMenu.Label(rect,"请退出战局后再修改角色数据。",InterfaceMenu.Accent);return;}
  if(!requested){requested=true;Begin(new(){{"action","status"}});}
  if(request?.IsCompleted==true){try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var error)&&error.TryGetInt32(out int code)&&code!=0)status=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";
   else{bool changed=ready;data=Data(root).Clone();ready=true;levels.Clear();wallet=data.GetProperty("balance").GetInt32().ToString();status=changed?"已保存。重新登录角色后刷新库存和角色面板。":"已读取角色数据。";}
  }catch{status="服务响应无法读取，请刷新。";}request=null;}
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width-250,30),page,InterfaceMenu.Accent,18);
  bool previous=GUI.enabled;GUI.enabled=previous&&request==null;
  if(InterfaceMenu.Button(new Rect(rect.xMax-230,rect.y,140,30),"热更新服务端"))Begin(new(){{"action","reload"}});
  if(InterfaceMenu.Button(new Rect(rect.xMax-80,rect.y,80,30),"刷新"))Begin(new(){{"action","status"}});
  var body=new Rect(rect.x,rect.y+48,rect.width,Math.Max(40,rect.height-100));
  if(ready){if(page=="钱包")Wallet(body);else List(body,page=="成就");}
  GUI.enabled=previous;InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-43,rect.width,43),status,null,12);
 }
 static void Wallet(Rect r){
  int balance=data.GetProperty("balance").GetInt32();long coins=data.GetProperty("coins").GetInt64();
  InterfaceMenu.Label(new Rect(r.x,r.y,r.width,32),$"商城余额  {balance:N0} TC",InterfaceMenu.Accent,19);
  InterfaceMenu.Label(new Rect(r.x,r.y+45,r.width,28),$"角色库存中的实体币  {coins:N0} TC");
  if(InterfaceMenu.Button(new Rect(r.x,r.y+88,230,36),"全部转入钱包",coins>0)&&coins>0)Begin(new(){{"action","deposit"},{"expectedBalance",balance},{"expectedCoins",coins}});
  InterfaceMenu.Paragraph(new Rect(r.x,r.y+136,r.width,48),"转入会扣除等量实体币。邮件里的币请先领取。",null,13);
  InterfaceMenu.Label(new Rect(r.x,r.y+205,120,30),"设置商城余额");wallet=InterfaceMenu.Field(new Rect(r.x+125,r.y+205,160,31),wallet);
  if(InterfaceMenu.Button(new Rect(r.x+300,r.y+205,90,31),"保存"))try{int target=ItemSpawnRules.Integer(wallet,0,int.MaxValue,"商城余额");Begin(new(){{"action","wallet"},{"value",target},{"expectedBalance",balance}});}catch(ArgumentException error){status=error.Message;}
 }
 static void List(Rect r,bool achievements){
  search=InterfaceMenu.Field(new Rect(r.x,r.y,r.width,30),search);
  var rows=data.GetProperty(achievements?"achievements":"skills").EnumerateArray().Select(entry=>{string id=entry.GetProperty("id").GetString()!;return (Entry:entry,Id:id,Name:Name(achievements?id+" name":id,id));}).Where(row=>search.Length==0||row.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||row.Id.Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();
  var view=new Rect(r.x,r.y+44,r.width,Math.Max(30,r.height-44));float width=view.width-25;
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,rows.Length*66));
  for(int i=Math.Max(0,(int)(scroll.y/66)-1);i<Math.Min(rows.Length,(int)((scroll.y+view.height)/66)+2);i++){var row=rows[i];float y=i*66;InterfaceMenu.Paragraph(new Rect(0,y,width-215,56),row.Name,null,13);
   if(achievements){bool unlocked=row.Entry.GetProperty("unlocked").GetBoolean();if(InterfaceMenu.Button(new Rect(width-155,y,155,32),unlocked?"已解锁 · 取消":"解锁",unlocked))Begin(new(){{"action","achievement"},{"id",row.Id},{"unlocked",!unlocked}});}
   else{string value=levels.TryGetValue(row.Id,out var edited)?edited:row.Entry.GetProperty("level").GetDouble().ToString("0.##",CultureInfo.InvariantCulture);value=InterfaceMenu.Field(new Rect(width-205,y,105,31),value);levels[row.Id]=value;if(InterfaceMenu.Button(new Rect(width-90,y,90,31),"保存等级"))try{double level=ItemSpawnRules.Number(value,0,51,"技能等级");Begin(new(){{"action","skill"},{"id",row.Id},{"value",level}});}catch(ArgumentException error){status=error.Message;}}
  }GUI.EndScrollView();
 }
}
