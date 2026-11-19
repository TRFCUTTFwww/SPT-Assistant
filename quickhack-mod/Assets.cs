using LocalBlackwallPulse;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LocalQuickhacks;

internal static class Assets {
    static readonly Dictionary<string,Texture2D> textures=[];
    static readonly Dictionary<string,AudioClip> clips=[];
    static readonly HashSet<string> missing=[];
    static GameObject? root;
    static AudioSource? audio;
    internal static string Home=Path.Combine(BepInEx.Paths.PluginPath,"CP2077Quickhacks");
    internal static void Initialize() {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        Texture("blackwall"); Clip("death"); Effects.WarmBlackwall();
        Plugin.Logger.LogInfo($"Blackwall resources prepared in {timer.Elapsed.TotalMilliseconds:F1} ms");
    }
    internal static Texture2D? Texture(string key) {
        if(textures.TryGetValue(key,out var value)) return value;
        string path=Path.Combine(Home,"Assets",key+".png");
        if(!File.Exists(path)) return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(texture); return null; }
        texture.name="Quickhack "+key; texture.wrapMode=TextureWrapMode.Clamp; textures[key]=texture;
        if(key=="blackwall") {
            // Work on a managed array, not millions of IL2CPP indexed accesses.
            var pixels=texture.GetPixels32().AsSpan().ToArray();
            BlackwallPixels.Normalize(pixels);
            texture.SetPixels32(new Il2CppStructArray<Color32>(pixels)); texture.Apply(false,true);
        }
        return texture;
    }
    static AudioClip? Clip(string key) {
        if(clips.TryGetValue(key,out var found))return found;
        string path=Path.Combine(Home,"Assets",key+".wav");
        if(!File.Exists(path)) { if(missing.Add(key)) Plugin.Logger.LogWarning("Missing optional quickhack sound: "+key); return null; }
        var wave=WavePcm.Read(path);
        var clip=AudioClip.Create("Quickhack "+key,wave.Samples.Length/wave.Channels,wave.Channels,wave.Rate,false);
        if(!clip.SetData(new Il2CppStructArray<float>(wave.Samples),0)) { UnityEngine.Object.Destroy(clip); throw new IOException("Cannot create WAV clip "+key); }
        clips[key]=clip; return clip;
    }
    internal static void Play(string key) {
        try {
            var clip=Clip(key); if(clip==null)return;
            if(root==null) { root=new GameObject("CP2077 Quickhack audio") { hideFlags=HideFlags.HideAndDontSave }; UnityEngine.Object.DontDestroyOnLoad(root); audio=root.AddComponent<AudioSource>(); audio.spatialBlend=0; audio.playOnAwake=false; }
            audio!.PlayOneShot(clip,Plugin.Volume.Value);
        } catch(Exception e) { Runtime.Warn("Audio",e); }
    }
    internal static void Clear() { HackUi.Clear(); if(root!=null) UnityEngine.Object.Destroy(root); root=null; audio=null; foreach(var t in textures.Values) UnityEngine.Object.Destroy(t); foreach(var c in clips.Values) UnityEngine.Object.Destroy(c); textures.Clear(); clips.Clear(); missing.Clear(); }
}

internal static class Effects {
    internal sealed record Marker(ActorRef Ref,HackKind Kind,float Start,float Until);
    static readonly List<Marker> markers=[];
    internal static IReadOnlyList<Marker> Markers=>markers;
    internal static void Forget(ActorRef reference,HackKind kind)=>markers.RemoveAll(m=>m.Ref==reference&&m.Kind==kind);
    internal static void ForgetAll()=>markers.Clear();
    sealed record Effect(EFT.Player Actor,HackKind Kind,float Start,float Duration,GameObject Root,Material Material,Transform[] Panels,Renderer[] Renderers);
    sealed record Visual(GameObject Root,Material Material,Transform[] Panels,Renderer[] Renderers);
    static readonly Stack<Visual> blackwallPool=[];
    static MaterialPropertyBlock? panelProperties;
    static readonly List<Effect> effects=[];
    static readonly List<Mesh> frames=[];
    internal static float PreviewTime=-100;
    internal static HackKind PreviewKind;
    internal static void Preview(HackKind kind) { PreviewKind=kind; PreviewTime=Time.unscaledTime; }
    internal static void Target(EFT.Player actor,HackKind kind,float effectDuration=3) {
        var reference=ActorRef.Of(actor);
        Forget(reference,kind);
        if(kind!=HackKind.BlackwallGateway) markers.Add(new(reference,kind,Time.unscaledTime,Time.unscaledTime+HackRules.Duration(kind)));
        while(markers.Count>128) markers.RemoveAt(0);
        var visual=kind==HackKind.BlackwallGateway&&blackwallPool.TryPop(out var cached)?cached:CreateVisual(kind);
        if(visual==null)return;
        var (root,material,panels,renderers)=visual; root.SetActive(true);
        effects.Add(new(actor,kind,Time.unscaledTime,effectDuration,root,material,panels,renderers));
        while(effects.Count>10) Drop(effects[0]);
    }
    internal static void WarmBlackwall() {
        if(blackwallPool.Count>0)return;
        var visual=CreateVisual(HackKind.BlackwallGateway);
        if(visual!=null) { visual.Root.SetActive(false); blackwallPool.Push(visual); }
    }
    static Visual? CreateVisual(HackKind kind) {
        var shader=Shader.Find("Sprites/Default"); if(shader==null) return null;
        var root=new GameObject("Quickhack effect") { hideFlags=HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(root);
        var material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
        material.mainTexture=kind==HackKind.BlackwallGateway?Assets.Texture("blackwall"):SkillVisuals.Texture(kind);
        material.SetInt("_ZWrite",0); material.SetInt("_ZTest",(int)UnityEngine.Rendering.CompareFunction.LessEqual);
        material.color=kind==HackKind.RebootOptics?new Color(.2f,1,1,.7f):kind==HackKind.Overheat?new Color(1,.35f,.015f,.8f):new Color(1,.06f,.1f,.8f);
        var panels=new Transform[SkillVisuals.Panels(kind)];
        var renderers=new Renderer[panels.Length];
        for(int i=0;i<panels.Length;i++) {
            GameObject p;
            if(kind==HackKind.BlackwallGateway) {
                MakeFrames(); p=new GameObject("Quickhack glitch");
                p.AddComponent<MeshFilter>().sharedMesh=frames[i%frames.Count]; p.AddComponent<MeshRenderer>();
            } else {
                p=GameObject.CreatePrimitive(PrimitiveType.Quad); p.name="Quickhack glitch";
                var c=p.GetComponent<Collider>(); if(c!=null) { c.enabled=false; UnityEngine.Object.Destroy(c); }
            }
            p.transform.SetParent(root.transform,false);
            renderers[i]=p.GetComponent<Renderer>(); renderers[i].sharedMaterial=material; panels[i]=p.transform;
        }
        return new(root,material,panels,renderers);
    }
    static void MakeFrames() {
        if(frames.Count>0) return;
        for(int row=0;row<3;row++) for(int col=0;col<2;col++) {
            float u0=col*.5f,u1=u0+.5f,v1=1-row*.25f,v0=v1-.25f;
            var mesh=new Mesh { name="Quickhack blackwall frame",hideFlags=HideFlags.HideAndDontSave };
            mesh.vertices=new Il2CppStructArray<Vector3>([new(-.5f,-.5f,0),new(.5f,-.5f,0),new(-.5f,.5f,0),new(.5f,.5f,0)]);
            mesh.uv=new Il2CppStructArray<Vector2>([new(u0,v0),new(u1,v0),new(u0,v1),new(u1,v1)]);
            mesh.triangles=new Il2CppStructArray<int>([0,2,1,2,3,1]); mesh.RecalculateBounds(); frames.Add(mesh);
        }
    }
    internal static void Tick() {
        if(effects.Count==0)return;
        var camera=Runtime.Camera; var rotation=camera?.transform.rotation;
        for(int effectIndex=effects.Count-1;effectIndex>=0;effectIndex--) {
            var e=effects[effectIndex];
            float age=Time.unscaledTime-e.Start;
            if(e.Actor==null||age>=e.Duration||!Runtime.InRaid) { Drop(e); continue; }
            var center=e.Kind switch {
                HackKind.BlackwallGateway or HackKind.Overheat or HackKind.WeaponGlitch => e.Actor.PlayerBones?.Ribcage?.Original?.position??e.Actor.Position+Vector3.up,
                HackKind.CrippleMovement => e.Actor.Position+Vector3.up*.35f,
                _=>e.Actor.PlayerBones?.Head?.Original?.position??e.Actor.Position+Vector3.up*1.6f
            };
            for(int i=0;i<e.Panels.Length;i++) {
                var p=e.Panels[i]; float phase=Mathf.Floor(age*9+i)*1.8f;
                if(e.Kind!=HackKind.BlackwallGateway) {
                    var pose=SkillVisuals.Pose(e.Kind,age,i,e.Duration);
                    p.position=center+e.Actor.transform.right*pose.Offset.x+Vector3.up*pose.Offset.y+e.Actor.transform.forward*pose.Offset.z;
                    p.localScale=pose.Size;
                    panelProperties??=new MaterialPropertyBlock();
                    panelProperties.Clear(); panelProperties.SetColor("_Color",pose.Color);
                    e.Renderers[i].SetPropertyBlock(panelProperties);
                    if(rotation.HasValue) p.rotation=rotation.Value;
                    continue;
                }
                p.position=center+new Vector3(Mathf.Sin(phase)*.22f,Mathf.Cos(phase*1.3f)*.65f,Mathf.Cos(phase)*.15f);
                p.localScale=new Vector3(.1f+Mathf.Abs(Mathf.Sin(phase))*.18f,.5f,1);
                if(rotation.HasValue) p.rotation=rotation.Value;
            }
            if(e.Kind==HackKind.BlackwallGateway) {
                var color=e.Material.color; color.a=(.3f+.5f*Mathf.Abs(Mathf.Sin(age*35)))*Math.Clamp((e.Duration-age)/Math.Min(1,e.Duration),0,1); e.Material.color=color;
            }
        }
    }
    static void Drop(Effect e) {
        effects.Remove(e);
        if(e.Kind==HackKind.BlackwallGateway&&blackwallPool.Count<10) { e.Root.SetActive(false); blackwallPool.Push(new(e.Root,e.Material,e.Panels,e.Renderers)); }
        else { UnityEngine.Object.Destroy(e.Root); UnityEngine.Object.Destroy(e.Material); }
    }
    internal static void Clear() { ForgetAll(); foreach(var e in effects.ToArray()) Drop(e); while(blackwallPool.TryPop(out var visual)) { UnityEngine.Object.Destroy(visual.Root); UnityEngine.Object.Destroy(visual.Material); } foreach(var mesh in frames) UnityEngine.Object.Destroy(mesh); frames.Clear(); SkillVisuals.Clear(); }
    internal static void PruneMarkers()=>markers.RemoveAll(m=>Time.unscaledTime>=m.Until||m.Ref.Resolve()==null);
}
