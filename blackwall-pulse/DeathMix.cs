namespace LocalBlackwallPulse;

// Mix the five selected Wwise event layers before submitting one clip to EFT.
internal static class DeathMix {
 internal static WavePcm Mix(WavePcm[] waves){
  if(waves.Length!=5||waves.Any(w=>w.Rate!=48000||w.Channels is <1 or >2||w.Samples.Length==0||w.Samples.Length%w.Channels!=0))throw new InvalidDataException("Unsupported blackwall death event samples");
  int frames0=waves[0].Samples.Length/waves[0].Channels,frames2=waves[2].Samples.Length/waves[2].Channels;
  int[] starts={0,frames0,9600,9600+frames2,0};float[] gains={.398f,.398f,.631f,.631f,1f};
  int frames=waves.Select((w,i)=>starts[i]+w.Samples.Length/w.Channels).Max();var mixed=new float[frames*2];
  for(int n=0;n<waves.Length;n++){var w=waves[n];int count=w.Samples.Length/w.Channels;for(int i=0;i<count;i++)for(int c=0;c<2;c++)mixed[(starts[n]+i)*2+c]+=w.Samples[i*w.Channels+(w.Channels==1?0:c)]*gains[n];}
  float peak=mixed.Max(x=>Math.Abs(x));float scale=peak>1?.98f/peak:1;for(int i=0;i<mixed.Length;i++)mixed[i]*=scale;
  return new(2,48000,mixed);
 }
}
