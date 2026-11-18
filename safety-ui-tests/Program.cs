using AssistantSafetyUi;
using UnityEngine;
var output=Path.GetFullPath(args.Length>0?args[0]:"safety-previews");Directory.CreateDirectory(output);
void Check(bool ok,string message){if(!ok)throw new Exception(message);}
var accent=new Color(.47f,.85f,.78f);
void Draw(SafetyPresentation view,int w,int h){Preview.Begin(w,h);SafetyScreen.Draw(view,accent,_=>{});Preview.Begin(w,h);SafetyScreen.Draw(view,accent,_=>{});foreach(var control in Preview.Controls.Where(c=>c.Fixed)){var r=control.Bounds;var l=new SafetyLayout(w,h);Check(r.y>=0&&(r.y+r.height)*l.Scale<=h+.1f,"Footer action outside viewport: "+control.Caption);Check(r.x>=0&&(r.x+r.width)*l.Scale<=w+.1f,"Action exceeds viewport width");}Check(Preview.ScrollDepth==0,"Unbalanced scroll view");}
var view=new SafetyPresentation{Failure="运行错误",Details="System.NullReferenceException: Object reference not set to an instance of an object.\n   at LocalSpt5Esp.Feature.Tick()\n   at LocalSpt5Esp.RuntimeModule.Update()",Recovery="当前不在战局，无需保存战局物品。",CanRollback=true,CanOpenLogs=true};
foreach(bool english in new[]{false,true})foreach(var size in new[]{(640,480),(800,600),(1024,768),(1280,720),(1920,1080),(2560,1440),(3440,1440),(480,640)}){view.English=english;Draw(view,size.Item1,size.Item2);}
Console.WriteLine("PASS actual error renderer footer bounds and balanced scrolling across sixteen language/viewport combinations");
view.English=false;Draw(view,1280,720);Preview.Save(Path.Combine(output,"error-zh-1280.png"));
view.English=true;view.ReloadFailure=true;Draw(view,1920,1080);Preview.Save(Path.Combine(output,"reload-en-1920.png"));
view.English=false;view.ReloadFailure=false;view.Recovery="SQLite 救援备份 snapshot";view.RecoveryBusy=true;Draw(view,480,640);Preview.Save(Path.Combine(output,"recovery-zh-480.png"));
var actions=new List<SafetyAction>();Preview.Click="重新启动游戏";SafetyScreen.Draw(view,accent,actions.Add);Check(!actions.Contains(SafetyAction.Restart),"Restart was possible during inventory recovery");
Preview.Click="退出游戏";SafetyScreen.Draw(view,accent,actions.Add);Check(!actions.Contains(SafetyAction.Close),"Exit was possible during inventory recovery");
view.RecoveryBusy=false;view.ReloadFailure=true;view.CleanupUnsafe=true;view.CanRollback=false;Draw(view,1280,720);Preview.Save(Path.Combine(output,"unsafe-zh-1280.png"));
Preview.Click="导出诊断日志";SafetyScreen.Draw(view,accent,actions.Add);Check(actions.Contains(SafetyAction.Export),"No usable primary action when rollback is unavailable");
actions.Clear();view.CleanupUnsafe=false;view.CanRollback=true;Preview.Click="回退到正常版本";SafetyScreen.Draw(view,accent,actions.Add);Check(actions.SequenceEqual(new[]{SafetyAction.Rollback}),"Primary rollback dispatch changed");
view.ReloadFailure=false;view.Simulation=true;Draw(view,1280,720);Preview.Click="结束测试，返回游戏";SafetyScreen.Draw(view,accent,actions.Add);Check(actions.Last()==SafetyAction.Continue,"Simulation cannot return to game");
view.Working=true;actions.Clear();Preview.Click="结束测试，返回游戏";SafetyScreen.Draw(view,accent,actions.Add);Check(actions.Count==0,"Busy operation permitted a second recovery action");
Console.WriteLine("PASS real renderer action dispatch, disabled recovery controls, usable fallback primary and controlled-test return");
view.Working=false;view.Simulation=false;view.English=true;view.Details=string.Join("\n",Enumerable.Repeat("System.InvalidOperationException: diagnostic line with a long error description",200));Draw(view,640,480);Preview.Click="Technical details";SafetyScreen.Draw(view,accent,_=>{});Draw(view,640,480);Preview.Save(Path.Combine(output,"details-en-640.png"));
GUI.enabled=false;GUI.depth=83;var originalColor=new Color(.3f,.4f,.5f);GUI.color=originalColor;var originalMatrix=GUI.matrix;SafetyScreen.Draw(view,accent,_=>{});Check(!GUI.enabled&&GUI.depth==83&&GUI.color==originalColor&&GUI.matrix==originalMatrix,"Error renderer leaked GUI state");
Console.WriteLine("PASS long technical details, independent scroll clipping and exact GUI state restoration");
