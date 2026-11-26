using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LocalSpt.Shared;

namespace LocalGearMail;

public sealed class ServerJsonModelDocs
{
    readonly Dictionary<string, string> summaries;
    readonly Dictionary<string, string> uniqueFields;
    readonly Dictionary<string, Type> configs;
    readonly Dictionary<Type, Dictionary<string, PropertyInfo>> properties = new();
    public ServerJsonModelDocs(Assembly assembly, string xmlPath)
    {
        summaries = File.Exists(xmlPath) ? XDocument.Load(xmlPath).Descendants("member")
            .Where(e => e.Attribute("name") != null && e.Element("summary") != null)
            .ToDictionary(e => e.Attribute("name")!.Value, e => Regex.Replace(string.Concat(e.Element("summary")!.Nodes().Select(n => n is XElement x && x.Name == "see" ? x.Attribute("cref")?.Value.Split('.').Last() : n.ToString())), "\\s+", " ").Trim()) : new();
        var types = assembly.GetTypes().Where(t => t.Namespace?.Contains(".Models.") == true).ToArray();
        configs = types.Where(t => t.Namespace == "SPTarkov.Server.Core.Models.Spt.Config" && t.Name.EndsWith("Config"))
            .GroupBy(t => t.Name[..^6], StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        uniqueFields = types.SelectMany(t => Props(t).Values).Select(p => (Key: p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name, Doc: Summary(p)))
            .Where(p => !string.IsNullOrWhiteSpace(p.Doc)).GroupBy(p => p.Key, StringComparer.Ordinal)
            .Where(g => g.Select(p => p.Doc).Distinct().Count() == 1).ToDictionary(g => g.Key, g => g.First().Doc!, StringComparer.Ordinal);
    }
    Dictionary<string, PropertyInfo> Props(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (!properties.TryGetValue(type, out var props)) properties[type] = props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetIndexParameters().Length == 0).GroupBy(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return props;
    }
    string? Summary(PropertyInfo p) => summaries.GetValueOrDefault("P:" + p.DeclaringType!.FullName!.Replace('+', '.') + "." + p.Name);
    public string? Describe(string file, string path)
    {
        var parts = JsonEditPath.Parse(path);
        if (file.StartsWith("SPT_Data/configs/", StringComparison.OrdinalIgnoreCase) && configs.TryGetValue(Path.GetFileNameWithoutExtension(file), out var type))
        {
            string? lastDoc = null;
            foreach (var part in parts)
            {
                type = Nullable.GetUnderlyingType(type) ?? type;
                var dict = type.GetInterfaces().Append(type).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>));
                var list = type.GetInterfaces().Append(type).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (dict != null) { type = dict.GetGenericArguments()[1]; continue; }
                if (part.Key == null && list != null) { type = list.GetGenericArguments()[0]; continue; }
                if (part.Key != null && Props(type).TryGetValue(part.Key, out var property)) { lastDoc = Summary(property); type = property.PropertyType; }
                else { lastDoc = null; break; }
            }
            if (!string.IsNullOrWhiteSpace(lastDoc)) return lastDoc;
        }
        string? key = parts.LastOrDefault().Key;
        return key == null ? null : uniqueFields.GetValueOrDefault(key);
    }
}
