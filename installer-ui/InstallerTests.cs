using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using SptAssistantSetup;
internal static class InstallerTests {
 static int checks;static string dummy,work;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static string Digest(string file){using(var data=File.OpenRead(file))return InstallerEngine.Hash(data);}
 static void Put(string root,string relative,string text){string path=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
 static string Fixture(string suffix,bool previous){
  string root=Path.Combine(work,"fixture 游戏 O'Brien $() "+suffix+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);File.Copy(dummy,Path.Combine(root,"EscapeFromTarkov.exe"));
  foreach(string file in new[]{"BepInEx/core/BepInEx.Unity.IL2CPP.dll","BepInEx/interop/Assembly-CSharp.dll","BepInEx/plugins/sptushonka/SPTushonka.Common.dll","SPT_Runtime/SPTarkov.Server.Core.dll"})Put(root,file,"dependency fixture");
  var items=new Dictionary<string,object>();var names=new Dictionary<string,string>();var prices=new Dictionary<string,int>();
  for(int i=0;i<110;i++){string id=i.ToString("x24");items[id]=new Dictionary<string,object>{{"_name","fixture item "+i},{"_props",new Dictionary<string,object>()}};names[id+" Name"]="测试物品 "+i;prices[id]=i+1;}
  var json=new JavaScriptSerializer();Put(root,"SPT_Runtime/SPT_Data/database/templates/items.json",json.Serialize(items));Put(root,"SPT_Runtime/SPT_Data/database/templates/prices.json",json.Serialize(prices));Put(root,"SPT_Runtime/SPT_Data/database/locales/global/ch.json",json.Serialize(names));Put(root,"SPT_Runtime/SPT_Data/database/locales/global/en.json",json.Serialize(names));Put(root,"SPT_Runtime/user/profiles/fixture.json","{\"_id\":\"fixture\",\"Wallet\":123,\"case\":1,\"Case\":2}");Put(root,"BepInEx/config/local.fixture.cfg","existing settings 中文");
  if(previous){Put(root,"SPT_Runtime/user/mods/LocalGearMail/backups/profiles.sqlite","existing SQLite backup fixture");Put(root,"BepInEx/plugins/CP2077Quickhacks/Local.Cp2077.Quickhacks.dll","existing quickhack host");Put(root,"BepInEx/plugins/CP2077Quickhacks/runtime/Local.Cp2077.Quickhacks.Core.module","previous quickhack core");Put(root,"SPT_Runtime/user/mods/LocalGearMail/api-key.txt","fixture-existing-key");Put(root,"BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll","previous host");foreach(string module in new[]{"atm-swords","projecte","scp3x"})Put(root,"BepInEx/plugins/UniversalModHost/modules/"+module+"/module.json","previous module");Put(root,"SPT_Runtime/user/mods/ProjectE/previous.txt","legacy resources");Put(root,"SPT_Runtime/user/mods/AllTheModiumSwords/previous.txt","legacy swords");}
  return root;
 }
 static InstallResult Install(string root,bool only){return InstallerEngine.Run(root,only,"ch",false,delegate{});}
 [STAThread]static int Main(string[] args){
  try{
   if(args.Length==2&&args[0]=="--preview"){System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);using(var preview=new InstallerForm("",false,"ch"))preview.Preview(args[1]);Console.WriteLine("PREVIEW PASS");return 0;}
   System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);dummy=args[0];work=Path.GetFullPath(args[1]);if(!work.Contains("installer-test"))throw new Exception("Tests require an isolated installer-test directory");Directory.CreateDirectory(work);
   Check(InstallerEngine.PsLiteral("O'Brien $()") == "'O''Brien $()'","PowerShell arguments retain apostrophes and substitution characters as literal data");
   Check(InstallerEngine.QuoteArgument("C:\\game path\\")=="\"C:\\game path\\\\\"","Windows arguments quote spaces and trailing separators");
   Check(!InstallerEngine.IsWithin(work,Path.Combine(work,"..","outside"))&&!InstallerEngine.IsWithin(work,work+"-neighbor"),"path checks reject traversal and similar-prefix sibling directories");
   using(var memory=new MemoryStream()){
    using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){using(var writer=new StreamWriter(zip.CreateEntry("../../escape.txt").Open()))writer.Write("unsafe");}memory.Position=0;
    bool rejected=false;try{InstallerEngine.ExtractZip(memory,Path.Combine(work,"extract"));}catch(InvalidDataException){rejected=true;}Check(rejected&&!File.Exists(Path.Combine(work,"escape.txt")),"archive extraction rejects traversal before writing outside its private directory");
   }
   string full=Fixture("full",true);string profile=Digest(Path.Combine(full,"SPT_Runtime/user/profiles/fixture.json"));string settings=Digest(Path.Combine(full,"BepInEx/config/local.fixture.cfg"));
   Check(InstallerEngine.NormalizeGameRoot(Path.Combine(full,"EscapeFromTarkov.exe"))==full&&InstallerEngine.NormalizeGameRoot(Path.Combine(full,"SPT_Runtime"))==full,"EXE selection and a mistaken server subfolder resolve to the actual game root");
   var installed=Install(full,false);Check(installed.Ok,"full installation works in a path containing spaces, Chinese, an apostrophe and dollar syntax");
   Check(Digest(Path.Combine(full,"SPT_Runtime/user/profiles/fixture.json"))==profile&&Digest(Path.Combine(full,"BepInEx/config/local.fixture.cfg"))==settings,"existing profile bytes and configuration are preserved");
   Check(File.ReadAllText(Path.Combine(full,"SPT_Runtime/user/mods/LocalGearMail/api-key.txt"))=="fixture-existing-key","existing administration key is preserved");
   Check(File.ReadAllText(Path.Combine(full,"SPT_Runtime/user/mods/LocalGearMail/backups/profiles.sqlite"))=="existing SQLite backup fixture","upgrading preserves the existing SQLite backup database");
   Check(File.ReadAllBytes(Path.Combine(full,"BepInEx/plugins/CP2077Quickhacks/runtime/Local.Cp2077.Quickhacks.Core.module"))[0]==77&&File.ReadAllText(Path.Combine(installed.Backup,"BepInEx/plugins/CP2077Quickhacks/runtime/Local.Cp2077.Quickhacks.Core.module"))=="previous quickhack core","existing Quickhacks receives the new core and retains a rollback copy");
   Check(Directory.Exists(installed.Backup)&&File.Exists(Path.Combine(installed.Backup,"SPT_Runtime/user/profiles/fixture.json")),"full install creates a real profile backup");
   Check(File.Exists(Path.Combine(full,"BepInEx/plugins/LocalSpt5ESP/runtime/Local.Spt5.ESP.Core.module"))&&File.Exists(Path.Combine(full,"SPT_Runtime/user/mods/LocalRaidControls/LocalRaidControls.dll")),"client and server components are installed together in full mode");
   Check(!Directory.Exists(Path.Combine(full,"SPT_Runtime/user/mods/ProjectE"))&&!Directory.Exists(Path.Combine(full,"BepInEx/plugins/UniversalModHost/modules/projecte")),"legacy Minecraft code is moved outside active loader discovery");
   Check(File.Exists(Path.Combine(installed.Backup,"disabled-code/SPT_Runtime/user/mods/ProjectE/previous.txt")),"retired module resources remain recoverable in the backup");
   var again=Install(full,false);Check(again.Ok&&again.Backup!=installed.Backup&&File.ReadAllText(Path.Combine(full,"SPT_Runtime/user/mods/LocalGearMail/api-key.txt"))=="fixture-existing-key","reinstallation is supported without replacing keys or reactivating retired loaders");
   string client=Fixture("client",false);var clientResult=Install(client,true);Check(clientResult.Ok,"client-only installation succeeds");
   Check(!Directory.Exists(Path.Combine(client,"BepInEx/plugins/CP2077Quickhacks")),"optional Quickhacks is not installed into a game that lacks its host and resources");
   Check(!File.Exists(Path.Combine(client,"SPT_Runtime/user/mods/LocalGearMail/api-key.txt"))&&!Directory.Exists(Path.Combine(client,"SPT_Runtime/user/mods/LocalRaidControls")),"client-only mode does not install server mods or generate an admin key");
   string verify=Fixture("verify",false);var verified=InstallerEngine.Run(verify,false,"ch",true,delegate{});Check(verified.Ok&&!Directory.Exists(Path.Combine(verify,"mod-backups"))&&!Directory.Exists(Path.Combine(verify,"BepInEx/plugins/LocalSpt5ESP")),"verify-only runs without modifying game files");
   string corrupt=Fixture("corrupt",false),package=InstallerEngine.ExtractPackage();try{File.AppendAllText(Path.Combine(package,"payload/Local.Spt5.ESP.dll"),"damage");var damaged=InstallerEngine.RunBackend(package,corrupt,false,"ch",false,Path.Combine(work,"corrupt.log"),delegate{});Check(!damaged.Ok&&damaged.Code=="PACKAGE_DAMAGED"&&!Directory.Exists(Path.Combine(corrupt,"mod-backups")),"corrupted payload is rejected before backups or target changes");}finally{InstallerEngine.Cleanup(package);}
   string failed=Fixture("rollback",true);var original=Directory.GetFiles(failed,"*",SearchOption.AllDirectories).ToDictionary(file=>file,Digest);InstallResult failure;
   using(var locked=new FileStream(Path.Combine(failed,"BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){failure=Install(failed,false);}
   Check(!failure.Ok&&failure.RollbackSucceeded,"a real destination-file lock produces an installation failure with rollback");
   Check(original.All(pair=>File.Exists(pair.Key)&&Digest(pair.Key)==pair.Value),"rollback restores every pre-existing file including retired modules and profiles");
   Check(!File.Exists(Path.Combine(failed,"BepInEx/plugins/LocalSpt5ESP/install-manifest.json")),"failed installation never writes a success manifest");
   string missing=Path.Combine(work,"does-not-exist");Check(Install(missing,false).Code=="GAME_NOT_FOUND"&&!Directory.Exists(missing),"invalid selection gives a readable error without creating a game directory");
   string wrong=Fixture("wrong-version",false);File.WriteAllText(Path.Combine(wrong,"EscapeFromTarkov.exe"),"not the supported build");var wrongResult=Install(wrong,false);Check(!wrongResult.Ok&&wrongResult.Code=="WRONG_GAME_VERSION"&&!Directory.Exists(Path.Combine(wrong,"mod-backups")),"an incompatible game version is rejected before target writes");
   using(var form=new InstallerForm("",false,"ch")){foreach(string code in new[]{"GAME_NOT_FOUND","WRONG_GAME_VERSION","DEPENDENCIES_MISSING","GAME_RUNNING","PACKAGE_DAMAGED","ACCESS_DENIED","FILES_LOCKED","ROLLBACK_FAILED"})Check(form.Friendly(code).Length>15,"actionable Chinese error: "+code);form.Preview(Path.Combine(work,"installer-preview.png"));}
   File.WriteAllText(Path.Combine(work,"result.json"),new JavaScriptSerializer().Serialize(new{ok=true,checks=checks,preview=Path.Combine(work,"installer-preview.png")}),new UTF8Encoding(false));Console.WriteLine("Passed "+checks+" installer checks.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
