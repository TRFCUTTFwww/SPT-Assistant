using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LocalBlackwallPulse;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LocalPortalGun;

public sealed partial class PortalModule {
    readonly List<Object> assets=[];
    readonly Dictionary<string,AudioClip> clips=[];
    readonly Dictionary<IntPtr,(Renderer Renderer,bool Enabled)> hidden=[];
    GameObject? audioRoot,gunRoot;
    AudioSource? audio;
    Mesh gunMesh=null!;
    Material gunMaterial=null!,ringBlue=null!,ringOrange=null!;
    Texture2D idleTexture=null!;
    float nextHands;
    T Own<T>(T obj) where T:Object {assets.Add(obj);return obj;}
    static GameObject Root(string name) {
        var root=new GameObject(name){hideFlags=HideFlags.HideAndDontSave,layer=2};
        Object.DontDestroyOnLoad(root);return root;
    }
    static Material Material(string shader) {
        var found=Shader.Find(shader)??Shader.Find("Sprites/Default")??throw new InvalidOperationException("No portal shader available");
        var m=new Material(found){hideFlags=HideFlags.HideAndDontSave};
        m.SetInt("_Cull",0);return m;
    }
    void LoadAssets() {
        string home=Path.Combine(context.ModuleDirectory,"Assets");
        audioRoot=Own(Root("Portal gun audio"));audio=audioRoot.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;
        foreach(string name in new[]{"blue","orange","open_blue","open_orange","close","enter","exit","invalid","equip","unequip"}) {
            var pcm=WavePcm.Read(Path.Combine(home,name+".wav"));
            var clip=Own(AudioClip.Create("Portal "+name,pcm.Samples.Length/pcm.Channels,pcm.Channels,pcm.Rate,false));
            if(!clip.SetData(new Il2CppStructArray<float>(pcm.Samples),0))throw new IOException("Cannot load portal sound "+name);
            clips[name]=clip;
        }
        var texture=Own(new Texture2D(2,2,TextureFormat.RGBA32,false));
        if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(Path.Combine(home,"gun.png"))))throw new IOException("Cannot load portal gun texture");
        gunMesh=Own(ReadMesh(Path.Combine(home,"gun.mesh")));
        gunMaterial=Own(Material("Unlit/Texture"));gunMaterial.mainTexture=texture;
        // First-person model is always visible; world portals retain depth testing.
        gunMaterial.SetInt("_ZTest",(int)UnityEngine.Rendering.CompareFunction.Always);gunMaterial.renderQueue=3100;
        ringBlue=Own(Material("Sprites/Default"));ringBlue.color=new Color(.08f,.6f,1,1);
        ringOrange=Own(Material("Sprites/Default"));ringOrange.color=new Color(1,.38f,.025f,1);
        idleTexture=Own(new Texture2D(1,1,TextureFormat.RGBA32,false));idleTexture.SetPixel(0,0,new Color(.02f,.03f,.055f));idleTexture.Apply();
        gunRoot=Own(Root("Portal gun view model"));gunRoot.AddComponent<MeshFilter>().sharedMesh=gunMesh;gunRoot.AddComponent<MeshRenderer>().sharedMaterial=gunMaterial;gunRoot.SetActive(false);
    }
    static Mesh ReadMesh(string path) {
        using var r=new BinaryReader(File.OpenRead(path));
        if(new string(r.ReadChars(4))!="PGM1")throw new InvalidDataException("Portal mesh header");
        int vn=r.ReadInt32(),tn=r.ReadInt32();
        if(vn is <3 or >60000||tn is <3 or >300000||tn%3!=0)throw new InvalidDataException("Portal mesh bounds");
        var v=new Vector3[vn];var n=new Vector3[vn];var uv=new Vector2[vn];var t=new int[tn];
        for(int i=0;i<vn;i++)v[i]=new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        for(int i=0;i<vn;i++)n[i]=new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        for(int i=0;i<vn;i++)uv[i]=new(r.ReadSingle(),r.ReadSingle());
        for(int i=0;i<tn;i++){t[i]=r.ReadInt32();if(t[i]<0||t[i]>=vn)throw new InvalidDataException("Portal mesh index");}
        var mesh=new Mesh {name="Portal 2 prop LOD0"};
        try{mesh.vertices=new Il2CppStructArray<Vector3>(v);mesh.normals=new Il2CppStructArray<Vector3>(n);mesh.uv=new Il2CppStructArray<Vector2>(uv);mesh.triangles=new Il2CppStructArray<int>(t);mesh.RecalculateBounds();return mesh;}
        catch{Object.Destroy(mesh);throw;}
    }
    void Play(string key){if(audio!=null&&clips.TryGetValue(key,out var clip))audio.PlayOneShot(clip,settings.Volume);}
    sealed class Portal : IDisposable {
        internal Vector3 Center,SurfacePosition;
        internal Quaternion Rotation,SurfaceRotation;
        internal Collider Surface=null!;
        internal int Index;
        internal GameObject Root=null!,CameraRoot=null!;
        internal Mesh Mesh=null!;
        internal Material Material=null!;
        internal LineRenderer Ring=null!;
        internal Camera Camera=null!;
        internal RenderTexture? Texture;
        internal Vector3[] Vertices=[];
        internal Il2CppStructArray<Vector2>? Uv;
        internal float NextRender;
        public void Dispose(){
            if(Camera!=null)Camera.targetTexture=null;
            if(Root!=null)Object.Destroy(Root);if(CameraRoot!=null)Object.Destroy(CameraRoot);
            if(Texture!=null){Texture.Release();Object.Destroy(Texture);}if(Material!=null)Object.Destroy(Material);if(Mesh!=null)Object.Destroy(Mesh);
        }
    }
    Portal BuildPortal(Portal p) {
        try {
            p.Root=Root(p.Index==0?"Blue portal":"Orange portal");p.Root.transform.SetPositionAndRotation(p.Center,p.Rotation);
            const int sides=64;
            var vertices=new Vector3[sides+1];var triangles=new int[sides*3];var uv=new Vector2[sides+1];
            for(int i=0;i<sides;i++) {
                float a=i*MathF.PI*2/sides;
                vertices[i+1]=new(MathF.Cos(a)*PortalRules.HalfWidth,MathF.Sin(a)*PortalRules.HalfHeight,0);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%sides+1;triangles[i*3+2]=i+1;
            }
            p.Vertices=vertices;p.Uv=new Il2CppStructArray<Vector2>(uv);
            p.Mesh=new Mesh{name="Portal aperture"};p.Mesh.vertices=new Il2CppStructArray<Vector3>(vertices);p.Mesh.uv=p.Uv;p.Mesh.triangles=new Il2CppStructArray<int>(triangles);p.Mesh.RecalculateBounds();
            p.Material=Material("Unlit/Texture");p.Material.mainTexture=idleTexture;
            p.Root.AddComponent<MeshFilter>().sharedMesh=p.Mesh;p.Root.AddComponent<MeshRenderer>().sharedMaterial=p.Material;
            p.Ring=p.Root.AddComponent<LineRenderer>();p.Ring.useWorldSpace=false;p.Ring.loop=true;p.Ring.positionCount=sides;p.Ring.widthMultiplier=.045f;p.Ring.sharedMaterial=p.Index==0?ringBlue:ringOrange;
            p.Ring.SetPositions(new Il2CppStructArray<Vector3>(vertices.Skip(1).Select(v=>v+Vector3.forward*.018f).ToArray()));
            p.CameraRoot=Root("Portal view camera");p.Camera=p.CameraRoot.AddComponent<Camera>();p.Camera.enabled=false;
            return p;
        }catch{p.Dispose();throw;}
    }
    public void LateTick() {
        if(!context.IsActive||!valid||view==null)return;
        bool usable=Application.isFocused&&!Cursor.visible&&Cursor.lockState==CursorLockMode.Locked&&Time.timeScale>.001f;
        if(equipped&&usable&&gunRoot!=null) {
            // Poll infrequently for changed held weapons. Restore exactly the renderers we own.
            if(Time.unscaledTime>=nextHands){nextHands=Time.unscaledTime+.25f;HideHands();}
            foreach(var item in hidden.Values)if(item.Renderer!=null)item.Renderer.enabled=false;
            gunRoot.layer=view.gameObject.layer;
            // Pick a layer rendered by this camera, but omitted from portal views below.
            if((view.cullingMask&(1<<gunRoot.layer))==0)gunRoot.layer=0;
            float kick=Mathf.Max(0,1-(Time.unscaledTime-lastShot)/.18f);
            gunRoot.transform.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(.22f,-.23f,.57f-kick*.055f)),view.transform.rotation*Quaternion.Euler(-90+kick*5,0,0));
            gunRoot.transform.localScale=Vector3.one*.5f;gunRoot.SetActive(true);
        }else{gunRoot?.SetActive(false);RestoreHands();}
        for(int i=0;i<2;i++) {
            var p=portals[i];if(p==null)continue;
            if(!Alive(p)){ClearPortals();break;}
            p.Ring.widthMultiplier=.04f+.008f*Mathf.Sin(Time.unscaledTime*3);
            var other=portals[1-i];if(!settings.LiveView||other==null||!usable)continue;
            var local=Local(p,view.transform.position);var screen=view.WorldToViewportPoint(p.Center);
            if(local.z<=.01f||screen.z<=0||screen.x<-.2f||screen.x>1.2f||screen.y<-.3f||screen.y>1.3f||(p.Center-view.transform.position).sqrMagnitude>2500)continue;
            // Screen projected UVs make the aperture a window, rather than a stretched TV.
            for(int j=0;j<p.Vertices.Length;j++){var point=view.WorldToViewportPoint(p.Center+p.Rotation*p.Vertices[j]);p.Uv![j]=new(point.x,point.y);}
            p.Mesh.uv=p.Uv;
            if(Time.unscaledTime<p.NextRender)continue;
            p.NextRender=Time.unscaledTime+1f/settings.ViewFps;
            RenderPortal(p,other);
        }
    }
    void RenderPortal(Portal p,Portal other) {
        int width=settings.ViewSize,height=Math.Max(128,(int)(width/view!.aspect));
        if(p.Texture==null||p.Texture.width!=width||p.Texture.height!=height) {
            if(p.Texture!=null){p.Camera.targetTexture=null;p.Texture.Release();Object.Destroy(p.Texture);}
            p.Texture=new RenderTexture(width,height,24){name="Portal view",antiAliasing=1};p.Texture.Create();p.Material.mainTexture=p.Texture;p.Material.color=Color.white;
        }
        var camera=p.Camera;
        camera.CopyFrom(view);camera.enabled=false;camera.targetTexture=p.Texture;camera.allowHDR=false;camera.allowMSAA=false;camera.depth=-100;camera.farClipPlane=Math.Min(view.farClipPlane,150);camera.nearClipPlane=.025f;
        camera.cullingMask=view.cullingMask&~(1<<2);
        camera.transform.SetPositionAndRotation(Through(p,other,view.transform.position),Turn(p,other)*view.transform.rotation);
        camera.projectionMatrix=view.nonJitteredProjectionMatrix;
        Vector3 normal=other.Rotation*Vector3.forward;
        var point=camera.worldToCameraMatrix.MultiplyPoint(other.Center+normal*.035f);
        var n=camera.worldToCameraMatrix.MultiplyVector(normal).normalized;
        camera.projectionMatrix=camera.CalculateObliqueMatrix(new Vector4(n.x,n.y,n.z,-Vector3.Dot(point,n)));
        // No recursive cameras or held gun in the remote view. All switches restored on failure.
        bool visible=gunRoot?.activeSelf==true;
        try {if(visible)gunRoot!.SetActive(false);camera.Render();}
        catch(Exception e){settings.LiveView=false;context.Log(global::Local.ModHost.LogLevel.Warning,"Portal live view disabled after render error: "+e.Message);}
        finally {if(visible&&gunRoot!=null)gunRoot.SetActive(true);}
    }
    void HideHands() {
        var hands=player?.HandsController;
        if(hands==null)return;
        foreach(var renderer in hands.GetComponentsInChildren<Renderer>(true)) {
            if(renderer==null||hidden.ContainsKey(renderer.Pointer))continue;
            hidden[renderer.Pointer]=(renderer,renderer.enabled);renderer.enabled=false;
        }
    }
    void RestoreHands(){foreach(var item in hidden.Values)if(item.Renderer!=null)item.Renderer.enabled=item.Enabled;hidden.Clear();nextHands=0;}
    void DisposeAssets() {
        RestoreHands();
        for(int i=assets.Count-1;i>=0;i--)if(assets[i]!=null)Object.Destroy(assets[i]);
        assets.Clear();clips.Clear();audio=null;audioRoot=gunRoot=null;
    }
}
