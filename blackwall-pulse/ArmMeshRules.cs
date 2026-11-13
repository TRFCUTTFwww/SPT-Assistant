namespace LocalBlackwallPulse;

internal static class ArmMeshRules {
 internal static bool RightArmBone(string name){
  string clean=new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
  return new[]{"rupperarm","rforearm","rpalm","rdigit","rightarm","rightforearm","righthand"}.Any(clean.Contains);
 }
 internal static bool RightHandBone(string name){string clean=new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());return clean.EndsWith("rpalm",StringComparison.Ordinal)||clean.EndsWith("righthand",StringComparison.Ordinal);}
 internal static bool ArmWeight(int bone,float weight,bool[] arm)=>weight>.001f&&bone>=0&&bone<arm.Length&&arm[bone];
 internal static bool KeepBodyTriangle(bool a,bool b,bool c)=>!a&&!b&&!c;
 internal static int[] Triangles(int[] source,bool[] arm,bool keepArm){
  if(source.Length%3!=0||source.Any(i=>i<0||i>=arm.Length))throw new InvalidDataException("Invalid skinned triangle indices");
  var result=new List<int>();
  for(int i=0;i<source.Length;i+=3)if((arm[source[i]]||arm[source[i+1]]||arm[source[i+2]])==keepArm){result.Add(source[i]);result.Add(source[i+1]);result.Add(source[i+2]);}
  return result.ToArray();
 }
 // Native hide flags are a visibility preference, not evidence that a mesh or
 // its skeleton is unavailable. EFT keeps alternate arm rigs under one root.
 internal static int Score(string name,bool mesh,bool active,bool enabled,bool hidden,bool cameraLayer,bool bindsPalm){
  if(!mesh||!active||!enabled||!bindsPalm||name.StartsWith("Blackwall",StringComparison.Ordinal)||name.Contains("SPT Assistant",StringComparison.OrdinalIgnoreCase))return -1;
  int score=100+(hidden?0:1000)+(cameraLayer?300:0);
  if(name.Contains("hand",StringComparison.OrdinalIgnoreCase))score+=200;
  if(name.Contains("arm",StringComparison.OrdinalIgnoreCase))score+=100;
  if(name.Contains("lod0",StringComparison.OrdinalIgnoreCase))score+=20;
  if(name.Contains("lod1",StringComparison.OrdinalIgnoreCase)||name.Contains("lod2",StringComparison.OrdinalIgnoreCase))score-=40;
  return score;
 }
}
