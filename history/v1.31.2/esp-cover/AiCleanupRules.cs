using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal static class AiCleanupRules {
 internal static bool Allowed(bool local,bool ai,long expectedWorld,long currentWorld,string expectedProfile,string currentProfile)=>!local&&ai&&expectedWorld!=0&&expectedWorld==currentWorld&&expectedProfile.Length>0&&expectedProfile==currentProfile;
 internal static string[] AllSteps(IEnumerable<(string Name,Action Run)> steps){var failures=new List<string>();foreach(var step in steps)try{step.Run();}catch(Exception error){failures.Add(step.Name+"："+error.Message);}return failures.ToArray();}
 internal static bool BlockSpawn(bool noAi,bool sceneExclusive,bool manual)=>(noAi||sceneExclusive)&&!manual;
}
