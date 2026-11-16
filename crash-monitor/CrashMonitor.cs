using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using AssistantDiagnostics;
internal static class CrashMonitor {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=1024*1024};
 internal static string Text(IDictionary<string,object> map,string key,string fallback){object value;return map!=null&&map.TryGetValue(key,out value)&&value!=null?Convert.ToString(value,CultureInfo.InvariantCulture):fallback;}
 static bool Flag(IDictionary<string,object> map,string key,bool fallback){bool value;return Boolean.TryParse(Text(map,key,""),out value)?value:fallback;}
 static Dictionary<string,object> Read(string path){try{return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));}catch{return new Dictionary<string,object>();}}
 [STAThread]static void Main(string[] args){try{Run(args);}catch{/* A monitor must never terminate or block the game. */}}
 static void Run(string[] args){
  var options=new Dictionary<string,string>();for(int i=0;i+1<args.Length;i+=2)options[args[i]]=args[i+1];
  int pid;long start;if(!options.ContainsKey("--pid")||!Int32.TryParse(options["--pid"],out pid)||!Int64.TryParse(options["--start"],out start))return;
  Process game;try{game=Process.GetProcessById(pid);if(game.StartTime.ToUniversalTime().Ticks!=start||!String.Equals(game.ProcessName,"EscapeFromTarkov",StringComparison.OrdinalIgnoreCase))return;}catch{return;}
  var launch=new Dictionary<string,object>();try{using(var pipe=new NamedPipeClientStream(".",options["--pipe"],PipeDirection.In,PipeOptions.None)){pipe.Connect(15000);using(var reader=new StreamReader(pipe,Encoding.UTF8))launch=Json.Deserialize<Dictionary<string,object>>(reader.ReadLine()??"{}");}}catch{}
  game.WaitForExit();int exit=game.ExitCode;game.Dispose();var state=Read(options["--state"]);
  if(Flag(state,"restart",false)){Restart(launch);return;}if(Flag(state,"normal",false)&&String.IsNullOrEmpty(Text(state,"summary","")))return;
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new ErrorForm(options["--root"],state,launch,exit));
 }
 static void Restart(IDictionary<string,object> launch){string exe=Text(launch,"exe","");if(!File.Exists(exe)||!String.Equals(Path.GetFileName(exe),"EscapeFromTarkov.exe",StringComparison.OrdinalIgnoreCase)){MessageBox.Show("启动参数不可用，请通过 SPT 启动器重新启动游戏。","SPT Assistant");return;}Process.Start(new ProcessStartInfo(exe,Text(launch,"arguments","")){WorkingDirectory=Text(launch,"workingDirectory",Path.GetDirectoryName(exe)),UseShellExecute=false});}
 sealed class ErrorForm:Form {
  readonly string root;readonly IDictionary<string,object> state,launch;readonly TextBox detail;readonly Label status;readonly Button export;
  internal ErrorForm(string gameRoot,IDictionary<string,object> snapshot,IDictionary<string,object> recipe,int exit){root=gameRoot;state=snapshot;launch=recipe;
   Text="SPT Assistant · 游戏错误";BackColor=Color.FromArgb(18,45,64);ForeColor=Color.White;Opacity=.94;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;TopMost=true;Font=new Font("Microsoft YaHei UI",12);AutoScroll=true;
   var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(60,24,60,24),ColumnCount=1,RowCount=7,AutoScroll=true};Controls.Add(panel);
   panel.Controls.Add(new Label{Text=":(",Font=new Font("Segoe UI",76),AutoSize=true});
   panel.Controls.Add(new Label{Text="游戏进程已停止",Font=new Font(Font.FontFamily,25),AutoSize=true});
   string summary=CrashMonitor.Text(state,"summary","");if(summary.Length==0)summary="游戏异常退出。仅凭退出码无法判断是 MOD、游戏原生代码还是驱动导致。";
   panel.Controls.Add(new Label{Text=summary+"\n退出码：0x"+exit.ToString("X8"),AutoSize=true,MaximumSize=new Size(1200,0),Margin=new Padding(0,16,0,16)});
   detail=new TextBox{Text=LogBundle.Redact(CrashMonitor.Text(state,"details","没有捕获到托管异常堆栈；请导出日志进一步检查。")),ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Height=160,BackColor=BackColor,ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle};panel.Controls.Add(detail);
   panel.Controls.Add(new Label{Text="进程已结束，无法再执行游戏内幸存结算。背包是否保存需以服务端确认或备份记录为准。",AutoSize=true,MaximumSize=new Size(1200,0),Margin=new Padding(0,16,0,16)});
   var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};export=Button("导出日志",Export);buttons.Controls.Add(export);buttons.Controls.Add(Button("重启游戏",delegate{try{Restart(launch);Close();}catch(Exception e){status.Text="重启失败："+e.Message;}}));buttons.Controls.Add(Button("关闭提示",delegate{Close();}));panel.Controls.Add(buttons);
   status=new Label{AutoSize=true,MaximumSize=new Size(1200,0)};panel.Controls.Add(status);
  }
  Button Button(string text,EventHandler click){var result=new Button{Text=text,AutoSize=true,Padding=new Padding(16,10,16,10),BackColor=Color.FromArgb(35,75,95),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,8,16,8)};result.Click+=click;return result;}
  async void Export(object sender,EventArgs args){export.Enabled=false;status.Text="正在导出日志…";string report=detail.Text;try{string path=await Task.Run(delegate{return LogBundle.Export(root,Path.Combine(root,"BepInEx","plugins","LocalSpt5ESP","exports"),CrashMonitor.Text(state,"minimum","INFO"),Flag(state,"includeSpt",true),Flag(state,"includeGame",true),Optional("hardware.json",Flag(state,"includeHardware",false)),Optional("performance.json",Flag(state,"includePerformance",true)),report);});status.Text="日志已导出："+path;}catch(Exception e){status.Text="导出失败："+e.Message;}finally{export.Enabled=true;}}
  string Optional(string name,bool include){string path=Path.Combine(root,"BepInEx","plugins","LocalSpt5ESP","runtime",name);return include&&File.Exists(path)?File.ReadAllText(path,Encoding.UTF8):"";}
 }
}
