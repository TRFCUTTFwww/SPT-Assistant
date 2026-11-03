using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static class GroundNavigation {
 enum Destination { None,Loot,Exit }
 static ConfigEntry<bool> enabled=null!,showHud=null!;
 static ConfigEntry<float> horizon=null!,spacing=null!,width=null!;
 static ConfigEntry<KeyCode> toggleKey=null!;
 static Destination kind;
 static string targetKey="",targetName="",message="请选择一个物品或撤离点。",problem="";
 static IntPtr worldPointer;
 static Vector3 plannedOrigin,plannedGoal;
 static float nextPlan,nextVisual,nextError,remaining,distanceFromPath;
 static bool partial,arrived,forcePlan=true;
 static float lastPlannedAt;
 static NVector[] route=Array.Empty<NVector>();
 static NavMeshPath? nativePath;
 static Il2CppStructArray<float>? costs;
 static GameObject? visual;
 static LineRenderer? line;
 static MeshRenderer? arrowRenderer;
 static Mesh? arrowMesh;
 static Material? material;
 static Vector2 scroll;
 internal static bool WantsExitData=>enabled!=null&&enabled.Value&&RaidClock.Active;
 static NVector N(Vector3 p)=>new(p.x,p.y,p.z);
 static Vector3 U(NVector p)=>new(p.X,p.Y,p.Z);
 internal static void Initialize(){enabled=FeatureConfig.Bind("33. 路面导航","Enabled","启用路面导航",true);showHud=FeatureConfig.Bind("33. 路面导航","Show navigation HUD","显示导航信息窗",true);horizon=FeatureConfig.Bind("33. 路面导航","Draw distance","前方路线显示长度（米）",80f,"",new AcceptableValueRange<float>(15,200));spacing=FeatureConfig.Bind("33. 路面导航","Arrow spacing","路面箭头间距（米）",3f,"",new AcceptableValueRange<float>(1,8));width=FeatureConfig.Bind("33. 路面导航","Line width","路线宽度（米）",.12f,"",new AcceptableValueRange<float>(.04f,.35f));toggleKey=FeatureConfig.Bind("33. 路面导航","Toggle key","导航暂停／继续（Ctrl + 按键）",KeyCode.G);}
 internal static void ToLoot(string key,string name){if(!RaidClock.Active){message="请进入战局后选择目标。";return;}Clear();kind=Destination.Loot;targetKey=key;targetName=name;worldPointer=Singleton<GameWorld>.Instance.Pointer;enabled.Value=true;RaidLoot.SelectNavigationTarget(key);message="正在寻找可行走路线…";}
 static void ToExit(string key,string name){if(!RaidClock.Active){message="请进入战局后选择目标。";return;}Clear();kind=Destination.Exit;targetKey=key;targetName=name;worldPointer=Singleton<GameWorld>.Instance.Pointer;enabled.Value=true;message="正在寻找撤离路线…";}
 internal static void Clear(){kind=Destination.None;targetKey=targetName="";route=Array.Empty<NVector>();nextPlan=nextVisual=0;remaining=distanceFromPath=0;forcePlan=true;arrived=partial=false;problem="";message="导航已停止。";Hide();}
 static void Hide(){if(visual!=null)visual.SetActive(false);}
 static bool Target(out Vector3 position,out string state){position=default;state="";if(kind==Destination.Loot)return RaidLoot.TryNavigationTarget(targetKey,out position,out _);if(kind==Destination.Exit){if(ExfilDisplay.TryNavigationExit(targetKey,out var exit)&&exit!=null){position=exit.Position;state=exit.Available?"":exit.State;return true;}}return false;}
 internal static void Tick(GameWorld? world,Player? player){try{
  if(!RaidClock.Active||world==null||player==null){if(worldPointer!=IntPtr.Zero||kind!=Destination.None){Clear();worldPointer=IntPtr.Zero;DestroyVisual();}return;}
  if(worldPointer!=IntPtr.Zero&&worldPointer!=world.Pointer){Clear();worldPointer=IntPtr.Zero;DestroyVisual();return;}
  if(Hotkeys.Pressed(toggleKey.Value))enabled.Value=!enabled.Value;
  if(kind==Destination.None||!enabled.Value){Hide();return;}
  if(!Target(out var goal,out var state)){Clear();message="目标已被取走、撤离点已移除或场景已改变。";return;}
  float direct=Vector3.Distance(player.Position,goal);
  if(new Vector2(player.Position.x-goal.x,player.Position.z-goal.z).magnitude<=1.8f&&Math.Abs(player.Position.y-goal.y)<=2f){arrived=true;route=Array.Empty<NVector>();Hide();message="已到达目标附近"+(state.Length>0?" · "+state:kind==Destination.Exit?"，请确认撤离条件。":"，请交互拾取。");return;}
  if(arrived){arrived=false;nextPlan=0;}
  if(Time.unscaledTime>=nextPlan){nextPlan=Time.unscaledTime+2;if(forcePlan||Time.unscaledTime-lastPlannedAt>=8||route.Length<2||Vector3.Distance(player.Position,plannedOrigin)>2.5f||Vector3.Distance(goal,plannedGoal)>1.5f||distanceFromPath>2)Plan(player.Position,goal);}
  if(route.Length<2){Hide();return;}
  var progress=RouteGeometry.Project(route,N(player.Position));distanceFromPath=progress.Distance;remaining=Math.Max(0,RouteGeometry.Length(route)-progress.Along);
  if(remaining<1.2f){Hide();message=(partial?"已到可寻路区域末端":"已到目标附近的道路末端")+" · 目标距离 "+direct.ToString("F0")+" 米，请检查入口或目标高度。";return;}
  message=(partial?"部分路线":"路线已规划")+" · 路程 "+remaining.ToString("F0")+" 米"+(distanceFromPath>3?" · 偏离路线，正在重算":"")+(state.Length>0?" · "+state:"");
  if(Time.unscaledTime>=nextVisual){nextVisual=Time.unscaledTime+.18f;var points=RouteGeometry.Slice(route,progress,horizon.Value);Render(points);CheckObstacle(player.Position,progress);}
 }catch(Exception e){route=Array.Empty<NVector>();Hide();message="寻路暂不可用："+e.Message;Warn(e);}}
 static void Plan(Vector3 origin,Vector3 goal){
  forcePlan=false;lastPlannedAt=Time.unscaledTime;plannedOrigin=origin;plannedGoal=goal;route=Array.Empty<NVector>();partial=false;problem="";
  nativePath??=new NavMeshPath();costs??=new Il2CppStructArray<float>(Enumerable.Repeat(1f,32).ToArray());
  var types=new List<int>{0};for(int i=0;i<Math.Min(4,NavMesh.GetSettingsCount());i++){int id=NavMesh.GetSettingsByIndex(i).agentTypeID;if(!types.Contains(id))types.Add(id);}
  float bestGap=float.PositiveInfinity;bool sourceFound=false,targetFound=false;
  foreach(int type in types){if(!NavMesh.SamplePositionFilter(origin,out var from,2.5f,type,NavMesh.AllAreas)||Math.Abs(from.position.y-origin.y)>1.8f)continue;sourceFound=true;
   foreach(float radius in new[]{1.5f,3.5f}){if(!NavMesh.SamplePositionFilter(goal,out var to,radius,type,NavMesh.AllAreas)||Math.Abs(to.position.y-goal.y)>2.8f)continue;targetFound=true;
    nativePath.ClearCorners();
    if(!NavMesh.CalculatePathFilterInternal(from.position,to.position,nativePath,type,NavMesh.AllAreas,costs))continue;var corners=RouteGeometry.Clean(nativePath.corners.Select(N));if(corners.Length<2||nativePath.status==NavMeshPathStatus.PathInvalid)continue;
    bool complete=nativePath.status==NavMeshPathStatus.PathComplete;float gap=Vector3.Distance(U(corners[^1]),goal);int special=-1;for(int j=0;j<corners.Length-1;j++)if(RouteGeometry.SpecialConnection(corners[j],corners[j+1])){special=j;break;}
    if(special>=0){corners=corners.Take(special+1).ToArray();complete=false;if(corners.Length<2)continue;gap=Vector3.Distance(U(corners[^1]),goal);}
    if(!complete&&gap>=bestGap)continue;route=corners;partial=!complete;bestGap=gap;if(special>=0)problem="路线在特殊连接前停止，请手动通过。";if(complete){message="路线已规划";Plugin.Self.Log.LogInfo($"Navigation path: target={targetName}, agent={type}, corners={route.Length}, length={RouteGeometry.Length(route):F1}, endpoint gap={gap:F1}");return;}
   }
  }
  if(route.Length>=2){message="只能找到部分路线，末端需自行寻找通道。";return;}message=!sourceFound?"当前位置没有可用道路网格，请走回地面后重试。":!targetFound?"目标附近没有可用道路网格，可尝试靠近后重算。":"当前寻路网格没有连接到目标，无法生成路线。";
 }
 static void CheckObstacle(Vector3 origin,RouteGeometry.Projection progress){problem=partial?"末端可能需要门、楼梯或特殊通道。":"";int next=Math.Min(route.Length-1,progress.Segment+1);var destination=U(route[next]);var delta=destination-origin;float length=delta.magnitude;if(length<.2f)return;if(Physics.Raycast(origin+Vector3.up*.85f,delta.normalized,out var hit,Math.Min(3,length),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){var door=hit.collider?.GetComponentInParent<Door>();if(door!=null)problem=door.DoorState==EDoorState.Locked?"前方是锁门，请先解锁或寻找其他入口。":"前方有门，请手动开门通过。";}}
 static bool Visual(){if(visual!=null&&material!=null)return true;DestroyVisual();var shader=new[]{"Hidden/Internal-Colored","Unlit/Color","Sprites/Default"}.Select(Shader.Find).FirstOrDefault(s=>s!=null&&s.isSupported);if(shader==null){problem="路面材质不可用，保留导航信息。";return false;}material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=3100};material.color=new Color(.22f,1f,.74f,.88f);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)CompareFunction.LessEqual);material.SetInt("_Cull",(int)CullMode.Off);
  visual=new GameObject("Local.Spt5.GroundNavigation"){hideFlags=HideFlags.HideAndDontSave};visual.transform.position=Vector3.zero;var lineObject=new GameObject("Route Line"){hideFlags=HideFlags.HideAndDontSave};lineObject.transform.SetParent(visual.transform,false);line=lineObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.sharedMaterial=material;line.numCornerVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;var arrowObject=new GameObject("Route Arrows"){hideFlags=HideFlags.HideAndDontSave};arrowObject.transform.SetParent(visual.transform,false);arrowRenderer=arrowObject.AddComponent<MeshRenderer>();arrowRenderer.sharedMaterial=material;arrowRenderer.shadowCastingMode=ShadowCastingMode.Off;arrowRenderer.receiveShadows=false;arrowMesh=new Mesh{hideFlags=HideFlags.HideAndDontSave};arrowMesh.MarkDynamic();arrowObject.AddComponent<MeshFilter>().sharedMesh=arrowMesh;return true;
 }
 static void Render(NVector[] points){if(points.Length<2||!Visual()){Hide();return;}visual!.SetActive(true);var positions=points.Select(p=>U(p)+Vector3.up*.12f).ToArray();line!.startWidth=line.endWidth=width.Value;line.positionCount=positions.Length;line.SetPositions(new Il2CppStructArray<Vector3>(positions));material!.color=partial?new Color(1,.76f,.2f,.9f):new Color(.22f,1,.74f,.9f);line.startColor=line.endColor=Color.white;
  var vertices=new List<Vector3>();var triangles=new List<int>();foreach(var arrow in RouteGeometry.Arrows(points,spacing.Value)){var center=U(arrow.Point)+Vector3.up*.14f;var direction=U(arrow.Direction);var side=Vector3.Cross(Vector3.up,direction);var tip=center+direction*.48f;var middle=center-direction*.07f;var left=center-direction*.38f+side*.34f;var right=center-direction*.38f-side*.34f;int index=vertices.Count;vertices.AddRange(new[]{tip,left,middle,tip,middle,right});triangles.AddRange(new[]{index,index+1,index+2,index+3,index+4,index+5});}arrowMesh!.Clear();arrowMesh.vertices=new Il2CppStructArray<Vector3>(vertices.ToArray());arrowMesh.triangles=new Il2CppStructArray<int>(triangles.ToArray());arrowMesh.RecalculateBounds();
 }
 static string Instruction(Camera? camera,Player player){if(route.Length<2)return "";var p=RouteGeometry.Project(route,N(player.Position));int next=Math.Min(route.Length-1,p.Segment+1);var delta=U(route[next])-player.Position;if(Math.Abs(delta.y)>1&&new Vector2(delta.x,delta.z).magnitude<8)return delta.y>0?"沿箭头上楼／上坡":"沿箭头下楼／下坡";var forward=camera?.transform.forward??player.transform.forward;forward.y=0;delta.y=0;if(delta.sqrMagnitude<.1f)return "沿路面箭头前进";float angle=Vector3.SignedAngle(forward,delta,Vector3.up);return Math.Abs(angle)>130?"转身跟随路线":angle>35?"向右跟随路线":angle< -35?"向左跟随路线":"沿路面箭头前进";}
 internal static void DrawHud(Camera? camera,Player? player){if(!showHud.Value||!enabled.Value||kind==Destination.None||player==null||!RaidClock.Active)return;var rect=InterfaceMenu.Place("navigation",new Rect(Math.Max(12,Screen.width/2-190),Math.Max(12,Screen.height-172),380,72));InterfaceMenu.Panel(rect);InterfaceMenu.Label(new Rect(rect.x+10,rect.y+5,rect.width-20,24),"导航 · "+targetName,InterfaceMenu.Accent,13);InterfaceMenu.Label(new Rect(rect.x+10,rect.y+28,rect.width-20,20),message,null,11);InterfaceMenu.Label(new Rect(rect.x+10,rect.y+49,rect.width-20,20),problem.Length>0?problem:Instruction(camera,player),partial?Color.yellow:InterfaceMenu.Text,11);}
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"路面导航 · 试验版",InterfaceMenu.Accent,18);InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+38,rect.width,49),"绿色路线沿寻路网格绕行，箭头指向目标；黄色表示只能找到部分路线。不会自动移动角色。",null,12);
  float col=(rect.width-24)/3;if(InterfaceMenu.Button(new Rect(rect.x,rect.y+98,col,33),"导航到选中物品")){var selected=RaidLoot.NavigationSelection;if(selected!=null)ToLoot(selected.Key,selected.Name);else message="请先在「本局物品」选中高亮物品。";}
  if(InterfaceMenu.Button(new Rect(rect.x+col+12,rect.y+98,col,33),enabled.Value?"暂停导航 · Ctrl+"+toggleKey.Value:"继续导航 · Ctrl+"+toggleKey.Value,enabled.Value))enabled.Value=!enabled.Value;
  if(InterfaceMenu.Button(new Rect(rect.x+(col+12)*2,rect.y+98,col,33),"停止并清除目标"))Clear();
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+148,rect.width,67),(targetName.Length>0?"目标："+targetName+"\n":"")+message+(problem.Length>0?"\n"+problem:""),partial?Color.yellow:InterfaceMenu.Text,13);
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+226,155,30),"立即重新规划")){nextPlan=0;forcePlan=true;}InterfaceMenu.Label(new Rect(rect.x+172,rect.y+226,rect.width-172,30),"偏离路线、目标移动或道路变化时自动重算",null,12);
  InterfaceMenu.Label(new Rect(rect.x,rect.y+276,rect.width,26),"选择撤离点",InterfaceMenu.Accent,15);var exits=ExfilDisplay.NavigationRows.OrderBy(e=>e.Available?0:1).ThenBy(e=>e.Distance).ToList();var view=new Rect(rect.x,rect.y+316,rect.width,Math.Max(50,rect.height-395));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,Math.Max(50,exits.Count*73)));for(int i=0;i<exits.Count;i++){var exit=exits[i];float y=i*73;InterfaceMenu.Paragraph(new Rect(0,y,view.width-160,65),exit.Name+" · "+exit.Distance.ToString("F0")+" 米\n"+exit.State,exit.Available?InterfaceMenu.Text:Color.gray,12);if(InterfaceMenu.Button(new Rect(view.width-145,y+10,117,33),targetKey==exit.Id&&kind==Destination.Exit?"当前目标":"导航到这里",targetKey==exit.Id&&kind==Destination.Exit))ToExit(exit.Id,exit.Name);}if(exits.Count==0)InterfaceMenu.Label(new Rect(0,0,view.width-24,40),RaidClock.Active?"撤离点正在加载；可先选择物品导航。":"进入战局后读取撤离点。",null,12);GUI.EndScrollView();InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-63,rect.width,60),"路线依赖地图的寻路网格；门、跳跃、地雷区和特殊通道仍需自行判断。物品被拿走或切换战局时自动停止。",null,12);
 }
 static void DestroyVisual(){try{if(visual!=null)Object.Destroy(visual);if(arrowMesh!=null)Object.Destroy(arrowMesh);if(material!=null)Object.Destroy(material);}finally{visual=null;line=null;arrowRenderer=null;arrowMesh=null;material=null;}}
 internal static void Shutdown(){Clear();worldPointer=IntPtr.Zero;DestroyVisual();nativePath?.ClearCorners();nativePath=null;costs=null;}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}
}
