using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LocalQuickhacks;

// Independent Unity effects. Only Blackwall uses the extracted Blackwall atlas.
// These textures are generated locally; they are not exported REDengine particles.
internal static class SkillVisuals {
    static readonly Dictionary<HackKind,Texture2D> textures=[];
    internal static int Panels(HackKind kind)=>kind is HackKind.Overheat or HackKind.SynapseBurnout?8:kind==HackKind.RebootOptics?3:6;
    static float Line(float x,float y,float ax,float ay,float bx,float by,float width) {
        float dx=bx-ax,dy=by-ay,t=Math.Clamp(((x-ax)*dx+(y-ay)*dy)/Math.Max(.0001f,dx*dx+dy*dy),0,1);
        float distance=MathF.Sqrt(MathF.Pow(x-ax-t*dx,2)+MathF.Pow(y-ay-t*dy,2));
        return Math.Clamp(1-distance/width,0,1);
    }
    internal static Texture2D Texture(HackKind kind) {
        if(textures.TryGetValue(kind,out var found)) return found;
        const int width=64,height=128;
        var pixels=new Color[width*height];
        for(int j=0;j<height;j++) for(int i=0;i<width;i++) {
            float x=(i+.5f)/width,y=(j+.5f)/height,a=0;
            var rgb=Color.white;
            switch(kind) {
                case HackKind.Overheat:
                    float taper=.42f*(1-y)+.025f,center=.5f+MathF.Sin(y*9)*y*.12f;
                    a=Math.Clamp(1-MathF.Abs(x-center)/taper,0,1)*Math.Clamp(y*12,0,1)*(1-y);
                    rgb=new Color(1,.2f+.8f*a,.015f+.7f*a); break;
                case HackKind.RebootOptics:
                    a=Line(x,y,.05f,.5f,.95f,.5f,.035f)*(.6f+.4f*MathF.Sin(x*42)*MathF.Sin(x*42)); break;
                case HackKind.WeaponGlitch:
                    a=Math.Max(Line(x,y,.06f,.14f,.7f,.4f,.025f),Math.Max(Line(x,y,.7f,.4f,.3f,.62f,.025f),Line(x,y,.3f,.62f,.95f,.9f,.025f))); break;
                case HackKind.CrippleMovement:
                    a=Math.Max(Line(x,y,.15f,.2f,.85f,.2f,.025f),Math.Max(Line(x,y,.15f,.8f,.85f,.8f,.025f),Math.Max(Line(x,y,.15f,.2f,.15f,.8f,.025f),Line(x,y,.85f,.2f,.85f,.8f,.025f)))); break;
                case HackKind.MemoryWipe:
                    int cellX=i/8,cellY=j/12;
                    a=(cellX*13+cellY*7)%5<2&&i%8<6&&j%12<3?.8f:0; break;
                case HackKind.Cyberpsychosis:
                    a=Math.Max(Line(x,y,.5f,.5f,.17f,.1f,.018f),Math.Max(Line(x,y,.5f,.5f,.9f,.22f,.018f),Math.Max(Line(x,y,.5f,.5f,.8f,.92f,.018f),Line(x,y,.5f,.5f,.12f,.83f,.018f)))); break;
                case HackKind.SynapseBurnout:
                    a=Math.Max(Line(x,y,.2f,.02f,.65f,.35f,.02f),Math.Max(Line(x,y,.65f,.35f,.35f,.5f,.02f),Math.Max(Line(x,y,.35f,.5f,.8f,.98f,.02f),Line(x,y,.35f,.5f,.05f,.75f,.016f)))); break;
            }
            pixels[j*width+i]=new(rgb.r,rgb.g,rgb.b,a);
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false) { name="Quickhack independent "+kind,hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp };
        texture.SetPixels(new Il2CppStructArray<Color>(pixels)); texture.Apply();
        textures[kind]=texture; return texture;
    }
    internal static (Vector3 Offset,Vector3 Size,Color Color) Pose(HackKind kind,float age,int index,float duration) {
        float phase=index*2.399963f,fade=Math.Clamp((duration-age)/.3f,0,1);
        var color=new Color(1,.08f,.15f,.7f*fade);
        var offset=Vector3.zero; var size=new Vector3(.22f,.3f,1);
        switch(kind) {
            case HackKind.Overheat:
                float rise=(age*.9f+index*.137f)%1;
                offset=new(MathF.Sin(phase)*.22f,-.48f+rise*.9f,MathF.Cos(phase)*.17f);
                size=new(.12f+.08f*MathF.Sin(phase)*MathF.Sin(phase),.25f+.32f*rise,1);
                color=new(1,1,1,(1-rise)*fade*.9f); break;
            case HackKind.RebootOptics:
                offset=new(0,(index-1)*.04f,0); size=new(.28f,.085f,1); color=new(.1f,1,1,(.4f+.4f*MathF.Sin(age*16+index)*MathF.Sin(age*16+index))*fade); break;
            case HackKind.WeaponGlitch:
                offset=new(MathF.Sin(phase)*.15f,MathF.Cos(phase)*.08f,.28f); size=new(.12f,.2f,1); color=new(1,.78f,.2f,(MathF.Sin(age*43+index)>.1f?.9f:.05f)*fade); break;
            case HackKind.CrippleMovement:
                offset=new(index%2==0?-.13f:.13f,-.12f+(index/2)*.15f,0); size=new(.14f,.13f,1); color=new(1,.48f,.12f,.7f*fade); break;
            case HackKind.MemoryWipe:
                offset=new(MathF.Sin(phase)*.13f,(age*.22f+index*.04f)% .3f,0); size=new(.18f,.22f,1); color=new(.75f,.88f,1,(.3f+.4f*MathF.Sin(age*17+index)*MathF.Sin(age*17+index))*fade); break;
            case HackKind.Cyberpsychosis:
                offset=new(MathF.Sin(phase)*.14f,MathF.Cos(phase)*.12f,MathF.Sin(phase*.7f)*.12f); size=new(.26f,.25f,1); color=new(1,.05f,.16f,(.3f+.6f*MathF.Pow(MathF.Max(0,MathF.Sin(age*12+index)),4))*fade); break;
            case HackKind.SynapseBurnout:
                float burst=Math.Clamp(age/.55f,0,1),radius=.05f+burst*.3f;
                offset=new(MathF.Sin(phase)*radius,MathF.Cos(phase)*radius,0); size=new(.12f+.1f*burst,.23f,1);
                color=new(1,.2f+.8f*(1-burst),.3f+.7f*(1-burst),MathF.Pow(1-burst,2)); break;
        }
        return (offset,size,color);
    }
    internal static void Clear() { foreach(var texture in textures.Values) UnityEngine.Object.Destroy(texture); textures.Clear(); }
}
