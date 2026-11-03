using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
Console.OutputEncoding=Encoding.UTF8;
try{
 if(args.Length!=2||args[0] is not ("--verify" or "--install"))throw new ArgumentException("请通过游戏目录的「安装辅助模组1.9.0.cmd」运行。");
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
 bool gameRunning=Process.GetProcessesByName("EscapeFromTarkov").Length>0;string reloadStatus=Path.Combine(runtime,"hotreload-status.json");
 if(gameRunning){if(!File.Exists(reloadStatus))throw new InvalidOperationException("游戏正在运行且未检测到热更新加载器，请关闭游戏后再安装。");using var hostState=JsonDocument.Parse(File.ReadAllText(reloadStatus));var state=hostState.RootElement;if(!state.TryGetProperty("ready",out var ready)||!ready.GetBoolean()||!state.TryGetProperty("apiVersion",out var api)||api.GetInt32()!=1)throw new InvalidOperationException("热更新加载器尚未就绪，请关闭游戏后再安装。");}
 DateTime installStarted=DateTime.UtcNow;
 string backup=Target("_spt5_esp_fix/backup-installed-1.9.0-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(backup);
 string moduleTarget=Path.Combine(runtime,"Local.Spt5.ESP.Core.module"),pdbTarget=Path.Combine(runtime,"Local.Spt5.ESP.Core.pdb"),readmeTarget=Path.Combine(plugin,"README-zh.txt");
 var destinations=new[]{moduleTarget,pdbTarget,readmeTarget,manifest};
 foreach(string destination in destinations)if(File.Exists(destination))File.Copy(destination,Path.Combine(backup,Path.GetFileName(destination)),false);
 foreach(string relative in new[]{"BepInEx/config/local.spt5.esp.cfg","BepInEx/plugins/LocalSpt5ESP/item-presets.json"}){string existing=Target(relative);if(File.Exists(existing))File.Copy(existing,Path.Combine(backup,Path.GetFileName(existing)),false);}
 bool changed=false;
 try{
  File.Copy(moduleSource,moduleTarget+".installing",true);if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(moduleTarget+".installing")))!=digest)throw new IOException("模块复制后校验失败。");
  changed=true;File.Move(moduleTarget+".installing",moduleTarget,true);File.Copy(pdbSource,pdbTarget,true);File.Copy(readmeSource,readmeTarget,true);
  document["version"]="1.9.0";document["coreSha256"]=digest;document["backup"]=backup;document["pendingGameLaunch"]=!gameRunning;document["liveReloadVerified"]=false;document["mainMenuDisplayVerified"]=false;document["inRaidTested"]=false;document["mailBackendVersion"]="1.5.0";
  File.WriteAllText(manifest+".installing",document.ToJsonString(new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));File.Move(manifest+".installing",manifest,true);
  bool liveVerified=false;
  if(gameRunning){Console.WriteLine("模块已替换，正在等待游戏热加载；请切回游戏保持运行。");for(int attempt=0;attempt<16;attempt++){System.Threading.Thread.Sleep(500);JsonObject? state=null;try{state=JsonNode.Parse(File.ReadAllText(reloadStatus))?.AsObject();}catch(IOException){}catch(JsonException){}if(state==null)continue;if(state["ready"]?.GetValue<bool>()==true&&state["coreVersion"]?.GetValue<string>()=="1.9.0"&&state["sha256"]?.GetValue<string>()==digest){liveVerified=true;break;}if(state["error"] is JsonValue error&&DateTime.TryParse(state["updatedUtc"]?.GetValue<string>(),out var updated)&&updated.ToUniversalTime()>=installStarted.AddSeconds(-1))throw new InvalidOperationException("游戏热加载失败："+error.ToString());}
   document["liveReloadVerified"]=liveVerified;File.WriteAllText(manifest,document.ToJsonString(new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));
   Console.WriteLine(liveVerified?"已确认：运行中的游戏加载了 1.9.0。":"尚未确认热加载；请切回游戏等待数秒，或按 Ctrl+F1 刷新。仍未更新时重启游戏。");}
  Console.WriteLine("安装完成：本地 SPT 控制中心 1.9.0");Console.WriteLine("备份位置："+backup);Console.WriteLine("启动游戏后按右 Shift；战局分类可设置剩余时间和定位物品。");File.WriteAllText(Path.Combine(package,"install-result.txt"),"Installed 1.9.0\nSHA256 "+digest+"\nBackup "+backup+"\n"+DateTime.Now.ToString("O"));
 }catch(Exception failure){if(changed){foreach(string destination in destinations){string original=Path.Combine(backup,Path.GetFileName(destination));if(File.Exists(original))try{File.Copy(original,destination,true);}catch(Exception rollback){Console.Error.WriteLine("回退需检查："+rollback.Message);}}}throw new IOException("安装未完成，已尝试恢复备份。备份目录："+backup,failure);}
 finally{foreach(string temporary in new[]{moduleTarget+".installing",manifest+".installing"})if(File.Exists(temporary))try{File.Delete(temporary);}catch{}}
}catch(Exception error){Console.Error.WriteLine("未完成："+error.Message);if(error.InnerException!=null)Console.Error.WriteLine(error.InnerException.Message);Environment.ExitCode=1;}
