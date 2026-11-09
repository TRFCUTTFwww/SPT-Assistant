using System;
using System.Linq;
using System.Numerics;
namespace LocalSpt5Esp;
internal enum ScenePose { Original,Standing,Crouched,Prone }
[Flags] internal enum SceneActions { None=0,LookAtPlayer=1,Jump=2,Nod=4,ShakeHead=8,FollowPlayer=16 }
internal sealed record ScenePoint(float X,float Y,float Z){internal Vector3 Vector=>new(X,Y,Z);}
internal sealed record BodyHealthPreset(float Maximum,float Current);
internal sealed record SceneEffectPreset(string Type,string Part,float Seconds,float Strength);
internal sealed record SceneBonePose(string Path,ScenePoint Position,float X,float Y,float Z,float W);
internal sealed record SceneLockedPose(ScenePoint Offset,float X,float Y,float Z,float W,SceneBonePose[] Bones);
internal sealed record ActorScenePreset {
 public bool Enabled{get;init;}
 public ScenePose Pose{get;init;}
 public float Yaw{get;init;}
 public float Pitch{get;init;}
 public bool SetFacing{get;init;}=true;
 public bool InitializeFacing{get;init;}
 public SceneActions Actions{get;init;}
 public bool NoDoors{get;init;}
 public bool NoHealing{get;init;}
 public bool NoMovement{get;init;}
 public float FollowDistance{get;init;}=3;
 public float Speed{get;init;}=1;
 public float ActionInterval{get;init;}=1.2f;
 public float ActionAmplitude{get;init;}=25;
 public bool Loop{get;init;}=true;
 public float Wait{get;init;}=1;
 public bool RelativeRoute{get;init;}=true;
 public string Map{get;init;}="";
 public ScenePoint[] Route{get;init;}=Array.Empty<ScenePoint>();
 public int StartPoint{get;init;}
 public float ActionPhase{get;init;}
}
internal static class DirectorRules {
 internal static void ValidateLocked(SceneLockedPose value){bool Point(ScenePoint p)=>p!=null&&float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);bool Rotation(float x,float y,float z,float w)=>float.IsFinite(x)&&float.IsFinite(y)&&float.IsFinite(z)&&float.IsFinite(w)&&double.IsFinite((double)x*x+(double)y*y+(double)z*z+(double)w*w)&&((double)x*x+(double)y*y+(double)z*z+(double)w*w)>.00001&&((double)x*x+(double)y*y+(double)z*z+(double)w*w)<100000000;if(value==null||!Point(value.Offset)||!Rotation(value.X,value.Y,value.Z,value.W)||value.Bones==null||value.Bones.Length is <1 or >2048||value.Bones.Any(b=>b==null||b.Path==null||b.Path.StartsWith('/')||b.Path.Split('/').Contains("..")||!Point(b.Position)||!Rotation(b.X,b.Y,b.Z,b.W)))throw new ArgumentException("已存模型姿势包含无效坐标、旋转或骨骼路径。");}
 internal static void ValidateHealth(System.Collections.Generic.Dictionary<string,BodyHealthPreset>? values){if(values==null)return;string[] parts={"Head","Chest","Stomach","LeftArm","RightArm","LeftLeg","RightLeg"};foreach(var pair in values)if(!parts.Contains(pair.Key)||pair.Value==null||!float.IsFinite(pair.Value.Maximum)||!float.IsFinite(pair.Value.Current)||pair.Value.Maximum<=0||pair.Value.Maximum>float.MaxValue/8||pair.Value.Current<0||pair.Value.Current>pair.Value.Maximum)throw new ArgumentException("各部位最大／当前血量无效："+pair.Key);}
 internal static ActorScenePreset Validate(ActorScenePreset value){
  if(!Enum.IsDefined(value.Pose)||(value.Actions&~(SceneActions)31)!=0)throw new ArgumentException("动作类型无效。");
  foreach(float number in new[]{value.Yaw,value.Pitch,value.FollowDistance,value.Speed,value.ActionInterval,value.ActionAmplitude,value.Wait,value.ActionPhase})if(!float.IsFinite(number))throw new ArgumentException("动作参数必须为有限数值。");
  if(value.Pitch is <-89 or >89||value.Speed is <.1f or >5||value.FollowDistance is <1 or >30||value.ActionInterval is <.3f or >60||value.ActionAmplitude is <0 or >80||value.Wait is <0 or >600)throw new ArgumentException("动作参数超出范围。");
  if(value.Route==null||value.Route.Length>256||value.Route.Any(p=>p==null||!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)))throw new ArgumentException("路线点无效或超过 256 个。");
  if(value.StartPoint<0||value.StartPoint>=Math.Max(1,value.Route.Length)||value.ActionPhase is <0 or >60)throw new ArgumentException("路线进度或动作相位无效。");
  // Retain the legacy file shape so existing scenes still load, but discard
  // retired actions and waypoints before any actor can execute them.
  return value with{Yaw=((value.Yaw%360)+360)%360,Actions=value.Actions&(SceneActions.LookAtPlayer|SceneActions.FollowPlayer),Route=Array.Empty<ScenePoint>(),StartPoint=0,ActionPhase=0};
 }
 internal static Vector2 Look(Vector3 direction,float previousYaw){if(direction.LengthSquared()<.000001f)return new(previousYaw,0);var d=Vector3.Normalize(direction);return new(MathF.Atan2(d.X,d.Z)*180/MathF.PI,-MathF.Asin(Math.Clamp(d.Y,-1,1))*180/MathF.PI);}
 internal static Vector2 LocalMove(Vector3 direction,float yaw){float a=yaw*MathF.PI/180;var flat=new Vector2(direction.X,direction.Z);if(flat.LengthSquared()<.000001f)return Vector2.Zero;flat=Vector2.Normalize(flat);return new(flat.X*MathF.Cos(a)-flat.Y*MathF.Sin(a),flat.X*MathF.Sin(a)+flat.Y*MathF.Cos(a));}
 internal static bool OwnsLocomotion(ActorScenePreset p)=>p.Enabled&&p.Actions.HasFlag(SceneActions.FollowPlayer);
}
