namespace LocalSpt5Esp;

internal readonly record struct BtrEventChanges(string Stop,bool Started=false,bool Arrived=false,bool Left=false);

internal sealed class BtrEventTracker {
 const float StationRadius=12;
 bool observed,driving,reportedLeave;
 string station="";
 float stationX,stationZ;

 internal void Reset(){
  observed=driving=reportedLeave=false;
  station="";
 }

 internal bool Arrive(string stop,float x,float z){
  if(stop.Length==0||(!driving&&station==stop))return false;
  observed=true;
  driving=reportedLeave=false;
  station=stop;
  stationX=x;
  stationZ=z;
  return true;
 }

 internal BtrEventChanges Observe(bool depot,bool stopped,bool moving,string stop,float x,float z){
  if(depot&&!moving){
   Reset();
   observed=true;
   return default;
  }
  if(!observed){
   observed=true;
   if(stopped)Arrive(stop,x,z);
   else driving=moving;
   return default;
  }
  if(stopped)return new(stop,Arrived:Arrive(stop,x,z));
  if(!moving)return default;

  bool started=!driving;
  driving=true;
  float dx=x-stationX,dz=z-stationZ;
  bool left=!reportedLeave&&station.Length>0&&dx*dx+dz*dz>=StationRadius*StationRadius;
  if(left)reportedLeave=true;
  return new(station,Started:started,Left:left);
 }
}
