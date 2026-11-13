using System.Text;
namespace LocalBlackwallPulse;
internal sealed record WavePcm(int Channels,int Rate,float[] Samples) {
 internal WavePcm WithGain(float gain){
  if(!float.IsFinite(gain)||gain<0)throw new ArgumentOutOfRangeException(nameof(gain));
  var output=new float[Samples.Length];for(int i=0;i<output.Length;i++){float value=Samples[i]*gain,level=Math.Abs(value);output[i]=level<=.8f?value:MathF.CopySign(.8f+.18f*(1-MathF.Exp(-(level-.8f)/.18f)),value);}
  return new(Channels,Rate,output);
 }
 internal static WavePcm Read(string path)=>Read(File.ReadAllBytes(path));
 internal static WavePcm Read(byte[] bytes){
  if(bytes.Length<12||Encoding.ASCII.GetString(bytes,0,4)!="RIFF"||Encoding.ASCII.GetString(bytes,8,4)!="WAVE")throw new InvalidDataException("Expected RIFF/WAVE");
  int format=0,channels=0,rate=0,bits=0,align=0;ReadOnlySpan<byte> data=default;
  for(int p=12;p+8<=bytes.Length;){string id=Encoding.ASCII.GetString(bytes,p,4);uint size=BitConverter.ToUInt32(bytes,p+4);int start=p+8;if(size>(uint)(bytes.Length-start))throw new InvalidDataException("Truncated WAV chunk");
   if(id=="fmt "){if(size<16)throw new InvalidDataException("Short WAV format");format=BitConverter.ToUInt16(bytes,start);channels=BitConverter.ToUInt16(bytes,start+2);rate=BitConverter.ToInt32(bytes,start+4);align=BitConverter.ToUInt16(bytes,start+12);bits=BitConverter.ToUInt16(bytes,start+14);if(format==65534&&size>=40)format=BitConverter.ToUInt16(bytes,start+24);}
   else if(id=="data")data=bytes.AsSpan(start,(int)size);
   long next=(long)start+size+(size&1);if(next>bytes.Length)break;p=(int)next;
  }
  if(channels is <1 or >8||rate is <8000 or >192000||bits is not (16 or 24 or 32)||align!=channels*(bits/8)||data.IsEmpty||data.Length%align!=0||format is not (1 or 3)||format==3&&bits!=32)throw new InvalidDataException("Unsupported WAV PCM format");
  int step=bits/8;var samples=new float[data.Length/step];
  for(int i=0;i<samples.Length;i++){var sample=data.Slice(i*step,step);float value=format==3?BitConverter.ToSingle(sample):bits==16?BitConverter.ToInt16(sample)/32768f:bits==32?BitConverter.ToInt32(sample)/2147483648f:((sample[0]|sample[1]<<8|sample[2]<<16)<<8>>8)/8388608f;if(!float.IsFinite(value))throw new InvalidDataException("Invalid WAV sample");samples[i]=Math.Clamp(value,-1,1);}
  return new(channels,rate,samples);
 }
}
