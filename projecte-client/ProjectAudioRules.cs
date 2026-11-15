namespace ProjectE.Client;
internal static class ProjectAudioRules {
 internal static bool ValidClip(bool alive,int samples,int channels,int rate,bool loaded)=>alive&&samples>0&&channels>0&&rate>0&&loaded;
}
