using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
namespace LocalSpt5Esp;
// Replace only the selected camera's color LUT inside its existing native
// post-processing pass. No extra fullscreen pass or live image readback.
internal static class Nw11Vision {
 const int Size=32;
 sealed record Grade(Texture2D Lut);
 static readonly Dictionary<NightVisionProfile,Grade> grades=new();
 static readonly Dictionary<IntPtr,int> rendered=new();
 static readonly CameraRenderValue<SphericalHarmonicsL2> ambient=new(()=>RenderSettings.ambientProbe,value=>RenderSettings.ambientProbe=value);
 static Camera.CameraCallback? before,after;static int frame=-1;static float nextError;static string error="";
 static readonly int lutId=Shader.PropertyToID("_Lut2D"),lutParams=Shader.PropertyToID("_Lut2D_Params");
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(ColorGradingRenderer),nameof(ColorGradingRenderer.Render)),postfix:new HarmonyMethod(typeof(Nw11Vision),nameof(RenderGrade)));
  plugin.Patches.Patch(AccessTools.Method(typeof(ColorGrading),nameof(ColorGrading.IsEnabledAndSupported)),prefix:new HarmonyMethod(typeof(Nw11Vision),nameof(GradingEnabled)));
 }
 internal static void Active(bool on){
  if(!on){Stop();return;}if(before!=null)return;
  before=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(Begin));
  after=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(End));
  Camera.onPreCull+=before;Camera.onPostRender+=after;
 }
 internal static void Frame(){if(frame==Time.frameCount)return;ambient.Reset();frame=Time.frameCount;}
 static void Begin(Camera camera){try{
  Frame();var profile=NightVisionAssist.RenderProfile(camera);
  ambient.Begin(camera.Pointer.ToInt64(),profile==null?null:value=>{value.AddAmbientLight(new Color(profile.Ambient,profile.Ambient,profile.Ambient,1));return value;});
 }catch(Exception e){Warn(e);}}
 static void End(Camera camera){try{ambient.End(camera.Pointer.ToInt64());}catch(Exception e){Warn(e);}}
 static bool GradingEnabled(PostProcessRenderContext __0,ref bool __result){if(NightVisionAssist.RenderProfile(__0.camera)==null)return true;__result=true;return false;}
 static void RenderGrade(PostProcessRenderContext __0){
  long stamp=RuntimePerformance.Start();try{
   var camera=__0.camera;if(camera==null)return;var profile=NightVisionAssist.RenderProfile(camera);if(profile==null)return;
   var grade=Resources(profile);var sheet=__0.uberSheet;
   sheet.DisableKeyword("COLOR_GRADING_HDR_2D");sheet.DisableKeyword("COLOR_GRADING_HDR_3D");sheet.EnableKeyword("COLOR_GRADING_LDR_2D");
   sheet.DisableKeyword("GRAIN");sheet.properties.SetTexture(lutId,grade.Lut);sheet.properties.SetVector(lutParams,new Vector4(1f/(Size*Size),1f/Size,Size-1,0));
   rendered[camera.Pointer]=rendered.GetValueOrDefault(camera.Pointer)+1;
  }catch(Exception e){Warn(e);}
  finally{RuntimeCosts.Mark(ref stamp,CostStage.Nw11Render);}
 }
 static Grade Resources(NightVisionProfile profile){
  if(grades.TryGetValue(profile,out var existing))return existing;
  Texture2D? lut=null;
  try{
   lut=new Texture2D(Size*Size,Size,TextureFormat.RGBA32,false,true){name="Nw11 clarity LUT",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
   var pixels=new Il2CppStructArray<Color>(Size*Size*Size);
   for(int b=0;b<Size;b++)for(int g=0;g<Size;g++)for(int r=0;r<Size;r++){
    var color=NightVisionRules.Pixel(profile,r/(float)(Size-1),g/(float)(Size-1),b/(float)(Size-1));
    pixels[b*Size+r+g*Size*Size]=new Color(color.R,color.G,color.B,1);
   }
   lut.SetPixels(pixels);lut.Apply(false,true);var result=new Grade(lut);
   // Retain the two cameras' recent grades, with bounded custom-edit history.
   if(grades.Count>=4){foreach(var old in grades.Values)Destroy(old);grades.Clear();}
   grades.Add(profile,result);error="";return result;
  }catch{if(lut!=null)UnityEngine.Object.Destroy(lut);throw;}
 }
 static void Warn(Exception e){error=e.Message;if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Nw11 rendering: "+e.Message);}
 internal static string Notice=>error;
 static void Stop(){
  ambient.Reset();if(before!=null)Camera.onPreCull-=before;if(after!=null)Camera.onPostRender-=after;before=after=null;frame=-1;
 }
 static void Destroy(Grade grade){if(grade.Lut!=null)UnityEngine.Object.Destroy(grade.Lut);}
 internal static void Reset(){try{Stop();}finally{foreach(var grade in grades.Values)Destroy(grade);grades.Clear();rendered.Clear();error="";}}
 internal static object Diagnostics()=>new{colorSpace=QualitySettings.activeColorSpace.ToString(),active=before!=null,ambientDepth=ambient.Depth,luts=grades.Count,cameras=rendered.Select(pair=>new{pointer=pair.Key.ToInt64(),passes=pair.Value}).ToArray(),error};
 // Offscreen shader check only. This never captures or changes a game frame.
 internal static object Check(){
  var previous=RenderTexture.active;Texture2D? source=null,read=null;RenderTexture? target=null;Material? material=null;CommandBuffer? command=null;var results=new List<object>();
  try{
   var camera=EFT.CameraControl.CameraManager.Instance?.Camera??Camera.main;
   var shader=camera?.GetComponent<PostProcessLayer>()?.m_Resources?.shaders?.uber??Shader.Find("Hidden/PostProcessing/Uber");
   if(shader==null||!shader.isSupported)throw new InvalidOperationException("Nw11 原生色调材质不可用");
   material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};material.EnableKeyword("COLOR_GRADING_LDR_2D");
   material.SetTexture("_AutoExposureTex",Texture2D.whiteTexture);material.SetFloat("_PostExposure",1);material.SetFloat("_LumaInAlpha",0);material.SetVector("_UVTransform",new Vector4(1,1,0,0));
   source=new Texture2D(4,1,TextureFormat.RGBAFloat,false,true);source.filterMode=FilterMode.Point;
   var inputs=new[]{new Color(0,0,0,1),new Color(.05f,.05f,.05f,1),new Color(.2f,.2f,.2f,1),new Color(.7f,.1f,.05f,1)};
   // The native uber pass consumes linear scene RGB, then performs its LDR
   // LUT lookup in sRGB. Convert the synthetic test colors accordingly.
   bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;source.SetPixels(inputs.Select(c=>linear?c.linear:c).ToArray());source.Apply();target=new RenderTexture(4,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();read=new Texture2D(4,1,TextureFormat.RGBAFloat,false,true);
   command=new CommandBuffer();
   foreach(var style in new[]{NightVisionStyle.Nw11Green,NightVisionStyle.Nw11Blue,NightVisionStyle.Nw11Day}){
    var profile=NightVisionRules.Device(style);var grade=Resources(profile);material.SetTexture(lutId,grade.Lut);material.SetVector(lutParams,new Vector4(1f/(Size*Size),1f/Size,Size-1,0));
    command.Clear();RuntimeUtilities.BlitFullscreenTriangle(command,source,new RenderTargetIdentifier(target),material,0,null,false,null);Graphics.ExecuteCommandBuffer(command);
    RenderTexture.active=target;read.ReadPixels(new Rect(0,0,4,1),0,0);read.Apply();var actual=read.GetPixels();float maximumError=0;
    for(int i=0;i<inputs.Length;i++){var expected=NightVisionRules.Pixel(profile,inputs[i].r,inputs[i].g,inputs[i].b);var output=QualitySettings.activeColorSpace==ColorSpace.Linear?actual[i].gamma:actual[i];float difference=Math.Max(Math.Abs(output.r-expected.R),Math.Max(Math.Abs(output.g-expected.G),Math.Abs(output.b-expected.B)));maximumError=Math.Max(maximumError,difference);if(difference>.035)throw new InvalidOperationException("Nw11 GPU lookup mismatch: "+style+" sample "+i+" error "+difference+" actual "+output+" expected "+expected);}
    results.Add(new{style=NightVisionRules.Name(style),passed=true,maximumError});
   }
   return new{passed=true,colorSpace=QualitySettings.activeColorSpace.ToString(),shader=shader.name,checks=results,state=Diagnostics()};
  }finally{RenderTexture.active=previous;command?.Release();if(material!=null)UnityEngine.Object.Destroy(material);if(source!=null)UnityEngine.Object.Destroy(source);if(read!=null)UnityEngine.Object.Destroy(read);if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}}
 }
}
