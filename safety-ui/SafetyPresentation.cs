using System;
namespace AssistantSafetyUi;
internal enum SafetyAction { None, Export, Rollback, Restart, Close, Continue, OpenLogs }
internal sealed class SafetyPresentation {
 internal string Failure="",Details="",Phase="",Recovery="",ExportStatus="";
 internal bool Simulation,ReloadFailure,CleanupUnsafe,Working,RecoveryBusy,CanRollback,CanOpenLogs;
 internal string Title=>Simulation?"错误保护测试已触发":ReloadFailure?"这次更新没有完成":"游戏遇到了问题，已为你暂停";
 internal string Subtitle=>Simulation?"这是一次受控测试。你的背包和战局不会因此改变。":ReloadFailure?"游戏会保持暂停。你可以回退到上个正常版本，或等待修复后继续更新。":"先查看下面的保存状态，再选择导出日志、回退功能或重新启动游戏。";
 internal string Reason {
  get {string text=Failure+"\n"+Details;if(text.Contains("NullReferenceException",StringComparison.OrdinalIgnoreCase))return "程序访问了一个还没有准备好的对象（空引用）。";
   if(text.Contains("IndexOutOfRange",StringComparison.OrdinalIgnoreCase)||text.Contains("ArgumentOutOfRange",StringComparison.OrdinalIgnoreCase))return "程序读取了超出有效范围的数据。";
   if(text.Contains("StackOverflow",StringComparison.OrdinalIgnoreCase))return Simulation?"正在模拟调用栈耗尽，未真正耗尽游戏线程。":"程序的调用层级超出了允许范围。";
   if(text.Contains("AccessViolation",StringComparison.OrdinalIgnoreCase))return Simulation?"正在模拟无效内存访问，未执行危险的内存操作。":"程序访问了无效的内存区域。";
   return ReloadFailure?"新的辅助功能未能完成加载或启用。":Failure.Length>0?Failure:"暂时无法确定具体原因，请导出日志进一步检查。";}
 }
 internal string RecoveryTitle=>Simulation?"本次测试不保存或结束战局":Recovery.Contains("SQLite 救援备份 ",StringComparison.Ordinal)?"已保留可恢复的物品备份":Recovery.Contains("不在战局",StringComparison.Ordinal)?"当前没有战局物品需要处理":RecoveryBusy?"正在保护本局物品，请稍候":ReloadFailure?"本局仍保持暂停":"物品保存状态";
 internal string RecoveryText=>Simulation?"你可以直接结束测试并返回游戏，无需重启。":ReloadFailure?"此次更新失败不会主动提交战局结算。继续等待修复时，游戏会保持暂停。":Recovery.Contains("游戏已退出战局",StringComparison.Ordinal)?"游戏已退出战局。请重新登录检查物品，若有缺失可使用救援备份恢复。":Recovery.Contains("SQLite 救援备份 ",StringComparison.Ordinal)?"物品快照已保存，正在尝试按幸存结束战局。最终结果以服务端存档为准。":RecoveryBusy?"正在为装备与背包保存恢复快照，随后尝试幸存结算。完成前请保持此窗口打开。":Recovery.Contains("不在战局",StringComparison.Ordinal)?"当前不在行动中，没有需要执行的战局结算。":"尚未确认保存结果。请先导出日志，保留现有备份。";
 internal string Advice=>CleanupUnsafe?"上个功能没有完成关闭，当前无法安全回退。建议导出日志后重新启动游戏。":RecoveryBusy?"正在等待物品备份或结算流程完成，重启和退出暂不可用。":Simulation?"测试只用于检查保护界面，不表示游戏真的发生了致命错误。":"物品备份和幸存结算是两个步骤；有备份不代表结算已经完成。";
}
