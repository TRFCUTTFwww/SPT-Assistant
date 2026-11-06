using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SPTushonka.Common.Http;
namespace LocalSpt5Esp;
internal static class GearMailClient {
 static string fingerprint="",deliveryId="";
 static bool previousSucceeded;
 internal static async Task<string> Send(List<Dictionary<string,object?>> items,bool customProperties){
  try{
   string digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(items))));
   if(previousSucceeded||digest!=fingerprint){fingerprint=digest;deliveryId="esp_"+Guid.NewGuid().ToString("N");}
   previousSucceeded=false;
   using var status=JsonDocument.Parse(await RequestHandler.PostJsonAsync("/local/gear/status","{}"));
   var capability=Data(status.RootElement);
   bool enhanced=capability.TryGetProperty("customItemProperties",out var support)&&support.ValueKind==JsonValueKind.True;
   if(customProperties&&!enhanced)return "邮件服务需重启服务端后才能支持自定义属性。";
   if(customProperties&&(!capability.TryGetProperty("extendedItemProperties",out var extended)||extended.ValueKind!=JsonValueKind.True))return "请更新并重启服务端。";
   string keyPath=Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","LocalGearMail","api-key.txt");
   var payload=new Dictionary<string,object?>{{"apiKey",File.ReadAllText(keyPath).Trim()},{"deliveryId",deliveryId},{"message","控制中心添加的物品已送达，请领取。"},{"items",items}};
   if(enhanced)payload["allowIncomplete"]=true;
   using var response=JsonDocument.Parse(await RequestHandler.PostJsonAsync("/local/gear/mail",JsonSerializer.Serialize(payload)));
   var root=response.RootElement;
   if(root.TryGetProperty("err",out var error)&&error.TryGetInt32(out int code)&&code!=0)return root.TryGetProperty("errmsg",out var message)?"邮件未发送："+message.GetString():"邮件服务拒绝了此请求。";
   var data=Data(root);
   if(!data.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True)return "邮件发送结果未确认；再次点击会使用同一发放编号重试。";
   previousSucceeded=true;
   return "物品已发到游戏邮件，请打开邮件领取。";
  }catch(Exception){return "邮件发送失败；请确认服务端已运行，再次点击可重试。";}
 }
 static JsonElement Data(JsonElement root)=>root.TryGetProperty("data",out var data)&&data.ValueKind==JsonValueKind.Object?data:root;
}
