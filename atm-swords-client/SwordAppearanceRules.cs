namespace AllTheModium.Client;
internal static class SwordAppearanceRules {
 internal static float Brightness(float percent)=>(float.IsFinite(percent)?Math.Clamp(percent,0,200):100)/100;
 internal static float Opacity(float percent)=>(float.IsFinite(percent)?Math.Clamp(percent,0,100):100)/100;
 internal static byte Scale(byte source,float multiplier)=>(byte)Math.Clamp((int)MathF.Round(source*multiplier),0,255);
}
