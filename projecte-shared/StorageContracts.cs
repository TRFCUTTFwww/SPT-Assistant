using System.Text.Json;
using System.Text.Json.Serialization;
namespace ProjectE;
public class StorageRequest {
 [JsonPropertyName("action")] public string Action{get;set;}="state";
 [JsonPropertyName("profile")] public string Profile{get;set;}="";
 [JsonPropertyName("revision")] public string Revision{get;set;}="";
 [JsonPropertyName("items")] public JsonElement? Items{get;set;}
}
