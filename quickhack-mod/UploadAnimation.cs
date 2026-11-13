namespace LocalQuickhacks;

// Parameters from Batch01 BOUND_UPLOAD_ANIMATION_TRACKS. Original coordinates
// use 2160p; this helper returns 1080p offsets. Gameplay upload time is separate.
internal static class UploadAnimation {
    internal const float CompleteDuration=.4159999641f;
    internal static float Unit(float t,float duration)=>Math.Clamp(t/duration,0,1);
    static float BackIn(float t)=>2.70158f*t*t*t-1.70158f*t*t;
    internal static (float Scale,float Y,float Alpha) Intro(float age) => (
        1.5f-.5f*BackIn(Unit(age,.4f)),
        -50+50*BackIn(Unit(age,.25f)),Unit(age,.03f));
    internal static (float Y,float Alpha) Complete(float age) {
        float y;
        if(age<.268249989f) y=12.5f*MathF.Pow(Unit(age,.268249989f),5);
        else if(age<.270249993f) y=12.5f;
        else { float t=Unit(age-.270249993f,.0797500014f)-1; y=12.5f*(1-MathF.Sqrt(Math.Max(0,1-t*t))); }
        return (y,1-Unit(age-.319249958f,.0967500061f));
    }
}
