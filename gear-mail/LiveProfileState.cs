using System.Reflection;
using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class LiveProfileState {
 static readonly PropertyInfo[] Properties=typeof(SptProfile).GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.CanRead&&p.CanWrite&&p.GetIndexParameters().Length==0).ToArray();
 // SaveServer.GetProfiles() returns a dictionary copy. Preserve the live object
 // identity so SaveServer and services holding that profile see the same state.
 public static void Replace(SptProfile target,SptProfile source){foreach(var property in Properties)property.SetValue(target,property.GetValue(source));}
 public static async Task RestoreAndSave(SptProfile target,SptProfile source,MongoId session,SaveServer saves,JsonUtil json){
  if(!ReferenceEquals(target,saves.GetProfile(session)))throw new InvalidOperationException("目标存档已被替换，请重新预览。");
  Replace(target,source);await saves.SaveProfileAsync(session,CancellationToken.None);
  string saved=await File.ReadAllTextAsync(Path.Combine("user","profiles",session+".json"),CancellationToken.None);
  if(!JsonNode.DeepEquals(JsonNode.Parse(json.Serialize(target)!),JsonNode.Parse(saved)))throw new IOException("存档写入核验失败。");
 }
}
