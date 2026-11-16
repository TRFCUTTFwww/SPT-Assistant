using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
[assembly:AssemblyTitle("SPT Assistant Setup")]
[assembly:AssemblyDescription("Offline graphical installer for SPT Assistant 2.3.1")]
[assembly:AssemblyCompany("SPT Assistant")]
[assembly:AssemblyProduct("SPT Assistant Setup")]
[assembly:AssemblyVersion("2.3.1.0")]
[assembly:AssemblyFileVersion("2.3.1.0")]
namespace SptAssistantSetup {
 internal static class Program {
  [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
  [STAThread]static int Main(string[] args){
   try{
    SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    string root="";bool client=false;string language=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="zh"?"ch":"en";
    for(int i=0;i<args.Length;i++){if(args[i]=="--game-root"&&i+1<args.Length)root=args[++i];else if(args[i]=="--client-only")client=true;else if(args[i]=="--language"&&i+1<args.Length)language=args[++i];}
    using(var form=new InstallerForm(root,client,language)){
     if(args.Length==2&&args[0]=="--preview"){form.Preview(args[1]);return 0;}
     using(var gate=new Mutex(false,@"Local\SPTAssistantSetup")){
      bool held;try{held=gate.WaitOne(0,false);}catch(AbandonedMutexException){held=true;}
      if(!held){MessageBox.Show("安装程序已经打开，请返回原来的安装窗口。\nSetup is already open. Please use the existing window.","SPT Assistant Setup",MessageBoxButtons.OK,MessageBoxIcon.Information);return 2;}
      try{Application.Run(form);}finally{gate.ReleaseMutex();}
     }
    }
    return 0;
   }catch(Exception error){MessageBox.Show("安装程序无法启动，请重新下载完整安装程序。\nSetup could not start. Please download the installer again.\n\n"+error.Message,"SPT Assistant Setup",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
  }
 }
}
