using AssistantSafetyUi;
internal static class SafetyPresentationChecks {
 internal static void Run(){
  void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  var view=new SafetyPresentation{English=true,Failure="未处理异常",Details="System.NullReferenceException",Recovery="SQLite 救援备份 backup-123",RecoveryBusy=true};
  Check(view.Reason.Contains("null reference")&&view.RecoveryText.Contains("being attempted"),"English reason or pending backup status is missing");
  view.RecoveryBusy=false;Check(view.RecoveryText.Contains("not confirmed")&&!view.RecoveryText.Contains("being attempted"),"finished backup falsely claims settlement is still running");
  view.Recovery+="\n救援等待超时；尚不能确认原生结算完成。";
  Check(view.RecoveryTitle=="Recovery needs attention"&&view.RecoveryText.Contains("could not be confirmed"),"timeout is masked by an existing backup receipt");
  view.Recovery="SQLite 救援备份 backup-123\n游戏已退出战局。";Check(view.RecoveryTitle=="The game has left the raid"&&!view.RecoveryText.Contains("being attempted"),"raid exit is masked by backup status");
  view.ReloadFailure=true;Check(view.RecoveryTitle=="The raid remains paused"&&view.RecoveryText.Contains("does not submit"),"reload failures claim a raid rescue");
  view.Simulation=true;Check(view.RecoveryText.Contains("No restart is needed")&&view.Advice.Contains("test"),"simulation lost its explicit safe return explanation");
  foreach(bool simulation in new[]{false,true})foreach(bool reload in new[]{false,true})foreach(string recovery in new[]{"","当前不在战局，无需保存战局物品。","SQLite 救援备份 backup-123","服务端备份失败","角色已阵亡","游戏已退出战局。"}){
   view.Simulation=simulation;view.ReloadFailure=reload;view.Recovery=recovery;
   foreach(string text in new[]{view.Title,view.Subtitle,view.Reason,view.RecoveryTitle,view.RecoveryText,view.Advice,view.ProgressText,view.ExportNotice})Check(!text.Any(c=>c is >= '\u3400' and <= '\u9fff'),"untranslated safety explanation: "+text);
  }
  view.ExportStatus="日志导出失败：disk full";Check(view.ExportFailed&&!view.ExportBusy&&view.ExportNotice.Contains("failed"),"export failure is not actionable");
  view.ExportStatus="正在导出日志…";Check(view.ExportBusy,"duplicate export is not gated");
  view.English=false;Check(view.Title.Any(c=>c is >= '\u3400' and <= '\u9fff'),"Chinese safety text was lost");
  foreach(var size in new[]{(640,480),(800,600),(1024,768),(1280,720),(1920,1080),(2560,1440),(3440,1440),(480,640)}){
   var layout=new SafetyLayout(size.Item1,size.Item2);
   Check(layout.X>=0&&layout.Y>=0&&layout.ContentHeight>80,"safety content is outside a supported viewport");
   Check((layout.X+layout.Width)*layout.Scale<=size.Item1+.01f&&(layout.Y+layout.Height)*layout.Scale<=size.Item2+.01f,"safety panel exceeds the viewport");
   float footerBottom=12+(layout.Stacked?154:54)+42+34;
   Check(footerBottom<layout.FooterHeight,"footer actions are clipped");
  }
  Console.WriteLine("PASS bilingual error explanations, backup/timeout/settlement precedence, export states and eight viewport layouts");
 }
}
