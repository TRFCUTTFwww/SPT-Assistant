using UnityEngine;

namespace LocalQuickhacks;
internal static class BlackwallPixels {
    internal static void Normalize(Color32[] pixels) {
        for(int i=0;i<pixels.Length;i++) {
            var c=pixels[i]; int brightness=Math.Max(c.r,Math.Max(c.g,c.b));
            pixels[i]=brightness==0?new Color32(0,0,0,0):new Color32((byte)(c.r*255/brightness),(byte)(c.g*255/brightness),(byte)(c.b*255/brightness),(byte)(c.a*brightness/255));
        }
    }
}
