namespace AllTheModium.Client;
internal static class InventoryIconRules {
 internal const int Cell=63;
 internal static int Pixels(int cells)=>checked(Cell*cells+1);
 internal sealed record Raster(int Width,int Height,uint[] Pixels);
 // Inverse nearest-neighbor projection uses the same x-y / x+y axes as the
 // sword mesh. A single scale fits both axes; source colors remain untouched.
 internal static Raster Build(int width,int height,uint[] source,bool sword){
  if(width<=0||height<=0||source.Length!=width*height)throw new ArgumentException("Invalid icon source");
  int targetWidth=Pixels(1),targetHeight=Pixels(sword?2:1);var result=new Raster(targetWidth,targetHeight,new uint[targetWidth*targetHeight]);
  double minU=double.PositiveInfinity,minV=minU,maxU=double.NegativeInfinity,maxV=maxU;
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)if((source[y*width+x]>>24)>=128){
   double u=sword?x-y:x,v=sword?x+y:y;
   minU=Math.Min(minU,u-(sword?1:0));maxU=Math.Max(maxU,u+1);minV=Math.Min(minV,v);maxV=Math.Max(maxV,v+(sword?2:1));
  }
  if(!double.IsFinite(minU))return result;
  double scale=Math.Min((targetWidth-8)/(maxU-minU),(targetHeight-8)/(maxV-minV)),centerU=(minU+maxU)/2,centerV=(minV+maxV)/2;
  for(int y=0;y<targetHeight;y++)for(int x=0;x<targetWidth;x++){
   double u=(x+.5-targetWidth/2.0)/scale+centerU,v=(y+.5-targetHeight/2.0)/scale+centerV;
   int sx=(int)Math.Floor(sword?(u+v)/2:u),sy=(int)Math.Floor(sword?(v-u)/2:v);
   if(sx<0||sx>=width||sy<0||sy>=height)continue;uint color=source[sy*width+sx];if((color>>24)>=128)result.Pixels[y*targetWidth+x]=color;
  }
  return result;
 }
}
