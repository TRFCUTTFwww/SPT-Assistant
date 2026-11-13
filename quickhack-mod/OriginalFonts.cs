using System.Text.Json;
using UnityEngine;

namespace LocalQuickhacks;

// Offline glyph rasterization avoids system font installation and importing
// REDengine rendFont data through Unity's runtime font loader.
internal static class OriginalFonts {
    sealed record Glyph(float X,float Y,float Width,float Height,float Left,float Top,float Advance);
    sealed record Atlas(string Key,float Pixels,float Ascent,float Descent,Dictionary<char,Glyph> Glyphs);
    static readonly Dictionary<int,Atlas?> atlases=[];
    internal static void Initialize() {
        for(int face=0;face<3;face++) {
            var atlas=Load(face);
            if(atlas!=null) Plugin.Logger.LogInfo($"Original font atlas ready: {atlas.Key}; {atlas.Glyphs.Count} glyphs");
        }
    }
    static Atlas? Load(int face) {
        if(atlases.TryGetValue(face,out var cached)) return cached;
        string key=face==1?"font_code":face==2?"font_latin":"font_body";
        try {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Assets.Home,"Assets",key+".json")));
            var root=doc.RootElement;
            float Number(JsonElement e,string name)=>e.GetProperty(name).GetSingle();
            var glyphs=new Dictionary<char,Glyph>();
            foreach(var property in root.GetProperty("glyphs").EnumerateObject()) {
                var g=property.Value;
                glyphs.Add((char)int.Parse(property.Name),new(Number(g,"x"),Number(g,"y"),Number(g,"width"),Number(g,"height"),Number(g,"left"),Number(g,"top"),Number(g,"advance")));
            }
            var atlas=new Atlas(key,Number(root,"pixels"),Number(root,"ascent"),Number(root,"descent"),glyphs);
            if(Assets.Texture(key)==null) throw new IOException("Missing font atlas "+key);
            atlases[face]=atlas;
            return atlas;
        } catch(Exception e) { Plugin.Logger.LogWarning("Original font fallback: "+key+" / "+e.Message); atlases[face]=null; return null; }
    }
    internal static bool Draw(Rect box,string value,Color color,int size,int face,TextAnchor align) {
        var atlas=Load(face); if(atlas==null) return false;
        // Player and NPC names may contain glyphs outside the built UI subset.
        // Let the existing dynamic font render the entire label in that case.
        foreach(char c in value) if(c!='\n'&&c!='\r'&&!atlas.Glyphs.ContainsKey(c)) return false;
        if(Event.current.type!=EventType.Repaint) return true;
        var texture=Assets.Texture(atlas.Key); if(texture==null) return false;
        float factor=size/atlas.Pixels;
        var lines=value.Replace("\r","").Split('\n');
        float lineHeight=size*1.25f,blockHeight=size+(lines.Length-1)*lineHeight;
        int vertical=(int)align/3,horizontal=(int)align%3;
        float y=vertical==1?(box.height-blockHeight)/2:vertical==2?box.height-blockHeight:0;
        // Glyph ink is placed relative to its baseline, with the usual line-box
        // ascent normalized to the requested UI height (not the full em box).
        float baseline=size*.86f;
        var saved=GUI.color;
        GUI.BeginGroup(box);
        try {
            GUI.color=color;
            foreach(string line in lines) {
                float width=0; foreach(char c in line) width+=atlas.Glyphs[c].Advance*factor;
                float x=horizontal==1?(box.width-width)/2:horizontal==2?box.width-width:0;
                foreach(char c in line) {
                    var g=atlas.Glyphs[c];
                    if(g.Width>0&&g.Height>0) {
                        var r=new Rect(x+g.Left*factor,y+baseline+(g.Top-atlas.Ascent)*factor,g.Width*factor,g.Height*factor);
                        var uv=new Rect(g.X/texture.width,1-(g.Y+g.Height)/texture.height,g.Width/texture.width,g.Height/texture.height);
                        GUI.DrawTextureWithTexCoords(r,texture,uv,true);
                    }
                    x+=g.Advance*factor;
                }
                y+=lineHeight;
            }
        } finally { GUI.EndGroup(); GUI.color=saved; }
        return true;
    }
    internal static void Clear()=>atlases.Clear();
}
