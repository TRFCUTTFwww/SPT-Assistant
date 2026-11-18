using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SPTushonka.Common.Http;
namespace LocalSpt5Esp;
internal static class WorldEditorClient {
 internal static async Task<JsonElement> Call(string action,string map,string template="",object? options=null,string role="",object? parameters=null){
  var payload=new Dictionary<string,object?>{{"action",action},{"map",map},{"template",template},{"role",role},{"options",options},{"apiKey",File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","LocalGearMail","api-key.txt")).Trim()}};
  payload["parameters"]=parameters;
  string response=await RequestHandler.PostJsonAsync("/local/gear/world-editor",JsonSerializer.Serialize(payload)).WaitAsync(TimeSpan.FromSeconds(30));
  using var doc=JsonDocument.Parse(response);var root=doc.RootElement;
  if(root.TryGetProperty("err",out var error)&&error.TryGetInt32(out int code)&&code!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var text)?text.GetString():"编辑器服务请求失败。");
  var data=root.TryGetProperty("data",out var value)?value:root;
  if(data.ValueKind!=JsonValueKind.Object||!data.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True)throw new ArgumentException("编辑器服务尚未就绪，请更新并重启服务端。");
  return data.Clone();
 }
}
