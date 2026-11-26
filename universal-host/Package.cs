using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Local.ModHost.Runtime;

internal sealed record ModuleManifest {
    public string Id {get;init;}="";
    public string Name {get;init;}="";
    public int ApiVersion {get;init;}=1;
    public string Version {get;init;}="";
    public string Revision {get;init;}="";
    public string EntryAssembly {get;init;}="";
    public string EntryType {get;init;}="";
    public bool Enabled {get;init;}=true;
    public Dictionary<string,string> Files {get;init;}=new();
}

internal sealed record Package(ModuleManifest Manifest,string Directory,Dictionary<string,byte[]> Code,string Fingerprint) {
    internal static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
    internal static bool ValidId(string value)=>Regex.IsMatch(value,"^[a-z0-9][a-z0-9._-]{0,63}$")&&value is not "." and not "..";
    internal static string Inside(string root,string relative) {
        if(string.IsNullOrWhiteSpace(relative)||relative.Contains('\\')||relative.Contains(':')||relative.StartsWith('/')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Invalid relative path: "+relative);
        string full=Path.GetFullPath(Path.Combine(root,relative));
        if(!full.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Path outside module");
        string current=root;
        foreach(string part in relative.Split('/')) {
            current=Path.Combine(current,part);
            if((File.Exists(current)||System.IO.Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Module paths cannot be links");
        }
        return full;
    }
    internal static Package Read(string moduleDirectory,byte[] manifestBytes) {
        var m=JsonSerializer.Deserialize<ModuleManifest>(manifestBytes,Json)??throw new InvalidDataException("Empty manifest");
        if(!ValidId(m.Id)||m.Id!=Path.GetFileName(moduleDirectory))throw new InvalidDataException("Manifest id must match its folder");
        if(m.ApiVersion!=1)throw new InvalidDataException("Unsupported host API: "+m.ApiVersion);
        if(!ValidId(m.Revision)||string.IsNullOrWhiteSpace(m.Version)||string.IsNullOrWhiteSpace(m.EntryType))throw new InvalidDataException("Missing version, revision or entry type");
        string release=Inside(moduleDirectory,"releases/"+m.Revision);
        var fingerprint=Convert.ToHexString(SHA256.HashData(manifestBytes));
        Dictionary<string,byte[]> code=new(StringComparer.OrdinalIgnoreCase);
        if(!m.Enabled)return new(m,release,code,fingerprint);
        if(m.Files==null||m.Files.Count==0||m.Files.Count>2048||!m.Files.ContainsKey(m.EntryAssembly)||!m.EntryAssembly.EndsWith(".module",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Entry must be a listed .module file");
        long total=0; HashSet<string> names=new(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in m.Files) {
            string file=Inside(release,pair.Key);
            if(!names.Add(file)||!Regex.IsMatch(pair.Value??"","^[a-fA-F0-9]{64}$"))throw new InvalidDataException("Duplicate path or invalid SHA256");
            var info=new FileInfo(file);
            if(!info.Exists)throw new FileNotFoundException("Package file missing",pair.Key);
            total+=info.Length;
            if(info.Length>256L*1024*1024||total>512L*1024*1024)throw new InvalidDataException("Package exceeds 512 MiB / 256 MiB per file limit");
            // Read into memory once; a publisher cannot race hash verification and code loading.
            byte[] bytes=File.ReadAllBytes(file);
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(pair.Value,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA256 mismatch: "+pair.Key);
            if(pair.Key.EndsWith(".module",StringComparison.OrdinalIgnoreCase)) {
                string key=Path.GetFileNameWithoutExtension(pair.Key);
                if(key.StartsWith("System.",StringComparison.OrdinalIgnoreCase)||key.StartsWith("UnityEngine",StringComparison.OrdinalIgnoreCase)||key.StartsWith("BepInEx",StringComparison.OrdinalIgnoreCase)||key.StartsWith("Il2Cpp",StringComparison.OrdinalIgnoreCase)||key is "Local.ModHost.Contracts" or "Local.UniversalModHost" or "0Harmony" or "Assembly-CSharp")throw new InvalidDataException("Shared runtime cannot be bundled: "+key);
                if(!code.TryAdd(key,bytes))throw new InvalidDataException("Duplicate managed library: "+key);
            } else if(pair.Key.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Use .module for private managed libraries; native DLL hot reload is unsupported");
        }
        return new(m,release,code,fingerprint);
    }
    internal IHotModule Prepare() {
        var context=new ModuleLoadContext(Code);
        var assembly=context.LoadFromStream(new MemoryStream(Code[Path.GetFileNameWithoutExtension(Manifest.EntryAssembly)]));
        // A normal BepInEx/IL2CPP plugin must not masquerade as a reloadable module.
        foreach(var type in assembly.GetTypes()) {
            if(type.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin"))throw new InvalidDataException("BepInPlugin entry points are not hot modules");
            for(var parent=type.BaseType;parent!=null;parent=parent.BaseType)if(parent.FullName=="UnityEngine.MonoBehaviour")throw new InvalidDataException("Modules cannot register generation-specific MonoBehaviours");
        }
        var entry=assembly.GetType(Manifest.EntryType,true)!;
        if(!typeof(IHotModule).IsAssignableFrom(entry)||entry.IsAbstract)throw new InvalidDataException("Entry must implement IHotModule");
        var module=(IHotModule)(Activator.CreateInstance(entry)??throw new InvalidDataException("Missing module instance"));
        if(module.ApiVersion!=1||module.Version!=Manifest.Version)throw new InvalidDataException("Module API/version differs from manifest");
        return module;
    }
    sealed class ModuleLoadContext(Dictionary<string,byte[]> code):AssemblyLoadContext("Mod-"+Guid.NewGuid().ToString("N"),false) {
        // Native detours can retain metadata. Cleanup releases live resources;
        // old assemblies deliberately remain resident until the game restarts.
        protected override Assembly? Load(AssemblyName name) {
            if(name.Name==typeof(IHotModule).Assembly.GetName().Name)return typeof(IHotModule).Assembly;
            if(name.Name!=null&&code.TryGetValue(name.Name,out var bytes))return LoadFromStream(new MemoryStream(bytes));
            return Default.Assemblies.FirstOrDefault(a=>a.GetName().Name==name.Name)??Default.LoadFromAssemblyName(name);
        }
    }
}
