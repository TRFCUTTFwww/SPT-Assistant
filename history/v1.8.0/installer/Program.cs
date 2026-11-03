using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
Console.OutputEncoding=Encoding.UTF8;
try{
 if(args.Length!=2||args[0] is not ("--verify" or "--install"))throw new ArgumentException("请通过游戏目录的「安装辅助模组1.8.0.cmd」运行。");
 string game=Path.GetFullPath(args[1]);string rootPrefix=Path.TrimEndingDirectorySeparator(game)+Path.DirectorySeparatorChar;
 string Target(string relative){string resolved=Path.GetFullPath(Path.Combine(game,relative));if(!resolved.StartsWith(rootPrefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("目标路径超出游戏目录。");return resolved;}
 if(!File.Exists(Target("EscapeFromTarkov.exe")))throw new FileNotFoundException("找不到游戏，请将安装包保留在原游戏目录。");
 string package=AppContext.BaseDirectory,payload=Path.Combine(package,"payload");
 string plugin=Target("BepInEx/plugins/LocalSpt5ESP"),runtime=Path.Combine(plugin,"runtime"),manifest=Path.Combine(plugin,"install-manifest.json");
 string moduleSource=Path.Combine(payload,"Local.Spt5.ESP.Core.module"),pdbSource=Path.Combine(payload,"Local.Spt5.ESP.Core.pdb"),readmeSource=Path.Combine(payload,"README-zh.txt");
 using var checks=JsonDocument.Parse(File.ReadAllText(Path.Combine(payload,"checksums.json")));
 foreach(var entry in checks.RootElement.EnumerateObject()){if(Path.GetFileName(entry.Name)!=entry.Name)throw new InvalidDataException("安装包文件名无效。");string input=Path.Combine(payload,entry.Name);if(!File.Exists(input)||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input)))!=entry.Value.GetString())throw new InvalidDataException("安装包校验失败："+entry.Name);}
 foreach(string input in new[]{moduleSource,pdbSource,readmeSource})if(!File.Exists(input))throw new FileNotFoundException("安装包不完整。",input);
 if(!File.Exists(Path.Combine(runtime,"Local.Spt5.ESP.Core.module"))||!File.Exists(manifest))throw new FileNotFoundException("找不到已安装的辅助模组，未修改文件。");
 var document=JsonNode.Parse(File.ReadAllText(manifest))?.AsObject()??throw new InvalidDataException("安装清单无效。");
 string digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(moduleSource)));
 if(args[0]=="--verify"){Console.WriteLine("PASS 安装包完整、文件哈希匹配、目标路径有效。只读检查，未安装。");return;}
 if(Process.GetProcessesByName("EscapeFromTarkov").Length>0)throw new InvalidOperationException("请先关闭游戏，再运行安装程序。");
 string backup=Target("_spt5_esp_fix/backup-installed-1.8.0-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(backup);
 string moduleTarget=Path.Combine(runtime,"Local.Spt5.ESP.Core.module"),pdbTarget=Path.Combine(runtime,"Local.Spt5.ESP.Core.pdb"),readmeTarget=Path.Combine(plugin,"README-zh.txt");
 var destinations=new[]{moduleTarget,pdbTarget,readmeTarget,manifest};
 foreach(string destination in destinations)if(File.Exists(destination))File.Copy(destination,Path.Combine(backup,Path.GetFileName(destination)),false);
 foreach(string relative in new[]{"BepInEx/config/local.spt5.esp.cfg","BepInEx/plugins/LocalSpt5ESP/item-presets.json"}){string existing=Target(relative);if(File.Exists(existing))File.Copy(existing,Path.Combine(backup,Path.GetFileName(existing)),false);}
 bool changed=false;
 try{
  File.Copy(moduleSource,moduleTarget+".installing",true);if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(moduleTarget+".installing")))!=digest)throw new IOException("模块复制后校验失败。");
  changed=true;File.Move(moduleTarget+".installing",moduleTarget,true);File.Copy(pdbSource,pdbTarget,true);File.Copy(readmeSource,readmeTarget,true);
  document["version"]="1.8.0";document["coreSha256"]=digest;document["backup"]=backup;document["pendingGameLaunch"]=true;document["liveReloadVerified"]=false;document["mainMenuDisplayVerified"]=false;document["inRaidTested"]=false;document["mailBackendVersion"]="1.5.0";
  File.WriteAllText(manifest+".installing",document.ToJsonString(new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));File.Move(manifest+".installing",manifest,true);
  Console.WriteLine("安装完成：本地 SPT 控制中心 1.8.0");Console.WriteLine("备份位置："+backup);Console.WriteLine("启动游戏后按右 Shift；战局分类可设置剩余时间和定位物品。");File.WriteAllText(Path.Combine(package,"install-result.txt"),"Installed 1.8.0\nSHA256 "+digest+"\nBackup "+backup+"\n"+DateTime.Now.ToString("O"));
 }catch(Exception failure){if(changed){foreach(string destination in destinations){string original=Path.Combine(backup,Path.GetFileName(destination));if(File.Exists(original))try{File.Copy(original,destination,true);}catch(Exception rollback){Console.Error.WriteLine("回退需检查："+rollback.Message);}}}throw new IOException("安装未完成，已尝试恢复备份。备份目录："+backup,failure);}
 finally{foreach(string temporary in new[]{moduleTarget+".installing",manifest+".installing"})if(File.Exists(temporary))try{File.Delete(temporary);}catch{}}
}catch(Exception error){Console.Error.WriteLine("未完成："+error.Message);if(error.InnerException!=null)Console.Error.WriteLine(error.InnerException.Message);Environment.ExitCode=1;}
