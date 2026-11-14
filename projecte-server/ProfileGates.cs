using System.Collections.Concurrent;
namespace ProjectE.Server;
internal static class ProfileGates {
 static readonly ConcurrentDictionary<string,SemaphoreSlim> gates=new();
 internal static SemaphoreSlim Get(string profile)=>gates.GetOrAdd(profile,_=>new(1,1));
}
