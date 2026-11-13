using UnityEngine;

namespace LocalQuickhacks;

internal static class RelicNotice {
    internal const float Duration=4;
    internal static float Started=float.NegativeInfinity;
    internal static bool Visible=>Runtime.Active&&Time.unscaledTime-Started<Duration;
    internal static void Show()=>Started=Time.unscaledTime;
    internal static void Clear()=>Started=float.NegativeInfinity;
}

internal static partial class HackUi {
    // Original localized warning and original atlas parts; layout/motion rebuilt in Unity.
    // The original warning widget and its bound animation have not yet been provided.
    static void DrawRelicNotice() {
        if(!RelicNotice.Visible) return;
        float age=Now-RelicNotice.Started;
        float alpha=Math.Clamp(age/.12f,0,1)*Math.Clamp((RelicNotice.Duration-age)/.3f,0,1);
        var red=Alpha(ScanRed,alpha);
        float width=470*(1-MathF.Pow(1-Math.Clamp(age/.22f,0,1),3));
        const float y=196;
        var bounds=new Rect(960-width/2,y,width,68);
        GUI.BeginGroup(bounds);
        try {
            Fill(new Rect(0,0,470,68),new Color(.06f,.008f,.014f,.88f*alpha));
            Image(new Rect(0,0,13,68),"relic_bracket",red,true);
            Line(13,0,470,0,Alpha(red,.7f)); Line(13,67,470,67,Alpha(red,.7f));
            Image(new Rect(24,16,36,36),"relic_icon",red);
            Text(new Rect(77,13,370,42),"检测到 Relic 故障",red,25,0,TextAnchor.MiddleLeft);
            if(age<.3f) Fill(new Rect(17,(age*83%1)*66,438,2),Alpha(red,.35f));
        } finally { GUI.EndGroup(); }
    }
}
