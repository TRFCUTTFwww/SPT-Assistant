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
    internal static string Home=>Path.Combine(BepInEx.Paths.PluginPath,"CP2077Quickhacks");
    internal static void Initialize() { }
    internal static Texture2D? Texture(string key) {
        if(textures.TryGetValue(key,out var value)) return value;
        string path=Path.Combine(Home,"Assets",key+".png");
        if(!File.Exists(path)) return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(texture); return null; }
        texture.name="Quickhack "+key; texture.wrapMode=TextureWrapMode.Clamp; textures[key]=texture;
        if(key=="blackwall") {
            var pixels=texture.GetPixels();
            for(int i=0;i<pixels.Length;i++) { var c=pixels[i]; float brightness=Mathf.Max(c.r,Mathf.Max(c.g,c.b)); pixels[i]=brightness>.00001f?new Color(c.r/brightness,c.g/brightness,c.b/brightness,c.a*brightness):Color.clear; }
            texture.SetPixels(pixels); texture.Apply();
        }
        return texture;
    }
    internal static void Play(string key) {
        try {
            if(!clips.TryGetValue(key,out var clip)) {
                string path=Path.Combine(Home,"Assets",key+".wav");
                if(!File.Exists(path)) { if(missing.Add(key)) Plugin.Logger.LogWarning("Missing optional quickhack sound: "+key); return; }
                var wave=WavePcm.Read(path);
                clip=AudioClip.Create("Quickhack "+key,wave.Samples.Length/wave.Channels,wave.Channels,wave.Rate,false);
                if(!clip.SetData(new Il2CppStructArray<float>(wave.Samples),0)) throw new IOException("Cannot create WAV clip "+key);
                clips[key]=clip;
            }
            if(root==null) { root=new GameObject("CP2077 Quickhack audio") { hideFlags=HideFlags.HideAndDontSave }; UnityEngine.Object.DontDestroyOnLoad(root); audio=root.AddComponent<AudioSource>(); audio.spatialBlend=0; audio.playOnAwake=false; }
            audio!.PlayOneShot(clip,Plugin.Volume.Value);
        } catch(Exception e) { Runtime.Warn("Audio",e); }
    }
    internal static void Clear() { if(root!=null) UnityEngine.Object.Destroy(root); root=null; audio=null; foreach(var t in textures.Values) UnityEngine.Object.Destroy(t); foreach(var c in clips.Values) UnityEngine.Object.Destroy(c); textures.Clear(); clips.Clear(); missing.Clear(); }
}

internal static class Effects {
    sealed record Effect(EFT.Player Actor,HackKind Kind,float Start,GameObject Root,Material Material,Transform[] Panels);
    static readonly List<Effect> effects=[];
    static readonly List<Mesh> frames=[];
    internal static float PreviewTime=-100;
    internal static HackKind PreviewKind;
    internal static void Preview(HackKind kind) { PreviewKind=kind; PreviewTime=Time.unscaledTime; }
    internal static void Target(EFT.Player actor,HackKind kind) {
        var shader=Shader.Find("Sprites/Default"); if(shader==null) return;
        var root=new GameObject("Quickhack effect") { hideFlags=HideFlags.HideAndDontSave };
        var material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
        material.mainTexture=Assets.Texture(kind==HackKind.BlackwallGateway?"blackwall":HackRules.Icon(kind));
        material.SetInt("_ZWrite",0); material.SetInt("_ZTest",(int)UnityEngine.Rendering.CompareFunction.LessEqual);
        material.color=kind==HackKind.RebootOptics?new Color(.2f,1,1,.7f):new Color(1,.06f,.1f,.8f);
        var panels=new Transform[6];
        for(int i=0;i<panels.Length;i++) {
            var p=GameObject.CreatePrimitive(PrimitiveType.Quad); p.name="Quickhack glitch"; p.transform.SetParent(root.transform,false);
            var c=p.GetComponent<Collider>(); if(c!=null) { c.enabled=false; UnityEngine.Object.Destroy(c); }
            if(kind==HackKind.BlackwallGateway) { MakeFrames(); p.GetComponent<MeshFilter>().sharedMesh=frames[i%frames.Count]; }
            p.GetComponent<Renderer>().sharedMaterial=material; panels[i]=p.transform;
        }
        effects.Add(new(actor,kind,Time.unscaledTime,root,material,panels));
        while(effects.Count>10) Drop(effects[0]);
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
        foreach(var e in effects.ToArray()) {
            float age=Time.unscaledTime-e.Start;
            if(e.Actor==null||age>3||!Runtime.InRaid) { Drop(e); continue; }
            var center=e.Kind==HackKind.BlackwallGateway?(e.Actor.PlayerBones?.Ribcage?.Original?.position??e.Actor.Position+Vector3.up):(e.Actor.PlayerBones?.Head?.Original?.position??e.Actor.Position+Vector3.up*1.6f);
            var camera=Runtime.Camera;
            for(int i=0;i<e.Panels.Length;i++) {
                var p=e.Panels[i]; float phase=Mathf.Floor(age*9+i)*1.8f;
                p.position=center+new Vector3(Mathf.Sin(phase)*.22f,Mathf.Cos(phase*1.3f)*(e.Kind==HackKind.BlackwallGateway?.65f:.15f),Mathf.Cos(phase)*.15f);
                p.localScale=new Vector3(.1f+Mathf.Abs(Mathf.Sin(phase))*.18f,e.Kind==HackKind.BlackwallGateway?.5f:.12f,1);
                if(camera!=null) p.rotation=camera.transform.rotation;
            }
            var color=e.Material.color; color.a=(.3f+.5f*Mathf.Abs(Mathf.Sin(age*35)))*Math.Clamp(3-age,0,1); e.Material.color=color;
        }
    }
    static void Drop(Effect e) { UnityEngine.Object.Destroy(e.Root); UnityEngine.Object.Destroy(e.Material); effects.Remove(e); }
    internal static void Clear() { foreach(var e in effects.ToArray()) Drop(e); foreach(var mesh in frames) UnityEngine.Object.Destroy(mesh); frames.Clear(); }
}
