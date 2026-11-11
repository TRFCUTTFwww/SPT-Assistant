using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace AssistantChat;
public static class ChatRelayPolicy {
 public static string[] Read(string json,string profile,string fingerprint){
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("admins",out var admins)||!root.TryGetProperty("blocked",out var blocked))throw new JsonException("Missing chat policy lists");
  bool Match(JsonElement list){if(list.ValueKind!=JsonValueKind.Array||list.GetArrayLength()>1024)throw new JsonException("Invalid chat policy list");bool match=false;foreach(var entry in list.EnumerateArray()){
   if(entry.ValueKind!=JsonValueKind.Object||!entry.TryGetProperty("profile",out var p)||p.ValueKind!=JsonValueKind.String||!entry.TryGetProperty("fingerprint",out var f)||f.ValueKind!=JsonValueKind.String)throw new JsonException("Invalid chat policy entry");
   string id=p.GetString()!,pin=f.GetString()!;if(id.Length is <1 or >80||pin.Length!=64||!pin.All(Uri.IsHexDigit))throw new JsonException("Invalid chat policy identity");
   if(id==profile&&pin.Equals(fingerprint,StringComparison.OrdinalIgnoreCase))match=true;
  }return match;}
  // Validate both lists in full before granting any permission.
  bool denied=Match(blocked),admin=Match(admins);return denied?Array.Empty<string>():admin?new[]{"chat.send","admin"}:new[]{"chat.send"};
 }
}
