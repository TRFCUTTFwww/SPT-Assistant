using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using Jint;
namespace LocalSpt5Esp;

// Runs only managed code on a worker. No Unity objects or CLR access are exposed.
internal static class CornerScript {
 internal sealed record TestRunResult(bool Ok,string Value,string Error,string[] Logs);
 internal const string Example="return \"SPT · \" + new Date().toLocaleTimeString();";
 internal const string AsyncExample="const url = new URL(\"https://example.com/api/status\");\nurl.search = new URLSearchParams({ page: 1 });\nconst response = await fetch(url);\nif (!response.ok) throw new Error(\"HTTP \" + response.status);\nconst data = await response.json();\nreturn data.version;";
 internal const string GameExample="if (!game.inRaid) return \"Hideout · Crafts:\" + game.hideoutCraftsReady + \" · FPS:\" + game.fps;\nreturn game.map + \" · \" + game.raidRemainingSeconds + \"s · AI:\" + game.aliveAi + \" · FPS:\" + game.fps;";
 internal const string VariableExample="const status = game.inRaid ? game.map + \" · AI:\" + game.aliveAi : \"Hideout · FPS:\" + game.fps;";
 internal static string Evaluate(string source,CancellationToken cancellation,Func<string,string>? fetch=null)=>EvaluateWithGame(source,cancellation,fetch,null);
 internal static string EvaluateWithGame(string source,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson)=>EvaluateDisplay(source,"",cancellation,fetch,gameJson);
 internal static string EvaluateDisplay(string source,string expression,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson)=>ExecuteCore(source,expression,cancellation,fetch,gameJson,null);
 internal static TestRunResult EvaluateTest(string source,CancellationToken cancellation,Func<string,string>? fetch=null,string? gameJson=null)=>EvaluateDisplayTest(source,"",cancellation,fetch,gameJson);
 internal static TestRunResult EvaluateDisplayTest(string source,string expression,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson){
  var logs=new List<string>();
  void Capture(string level,string message){if(logs.Count<200)logs.Add($"[{level}] {message}");else if(logs.Count==200)logs.Add("[LOG] (日志已达 200 行上限，后续已省略)");}
  try{
   string value=ExecuteCore(source,expression,cancellation,fetch,gameJson,Capture);
   return new(true,value,"",logs.ToArray());
  }catch(Exception e){
   return new(false,"",e.GetBaseException().Message,logs.ToArray());
  }
 }
 internal static string SourceWithExpression(string source,string expression){
  if(!string.IsNullOrWhiteSpace(expression))source+="\nreturn (\n"+expression+"\n);";
  if(source.Length>16384)throw new ArgumentException("脚本最多 16384 个字符。");
  return source;
 }
 static string ExecuteCore(string source,string expression,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson,Action<string,string>? log){
  source=SourceWithExpression(source,expression);
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(30));int calls=0;
  using var engine=new Engine(options=>options.MaxStatements(200000).LimitRecursion(64).LimitMemory(32*1024*1024).TimeoutInterval(TimeSpan.FromSeconds(30)).CancellationToken(deadline.Token));
  engine.SetValue("__cornerRequest",new Func<string,string,string>((url,options)=>{deadline.Token.ThrowIfCancellationRequested();if(++calls>64)throw new InvalidOperationException("每次刷新最多 64 个 HTTP 请求。");var reply=fetch!=null?new CornerScriptWeb.Reply(200,"OK",url,new(),fetch(url)):CornerScriptWeb.Send(url,options,deadline.Token);return JsonSerializer.Serialize(reply);}));
  engine.SetValue("__cornerParseUrl",new Func<string,string,string>(CornerScriptWeb.ParseUrl));engine.SetValue("__cornerUpdateUrl",new Func<string,string,string,string>(CornerScriptWeb.UpdateUrl));
  if(log!=null)engine.SetValue("__cornerLog",log);
  engine.Execute(CornerScriptWeb.Bootstrap+"\nglobalThis.cornerText = undefined;");
  if(!string.IsNullOrWhiteSpace(gameJson)){engine.SetValue("__cornerGameJson",gameJson);engine.Execute("globalThis.game = Object.freeze(JSON.parse(__cornerGameJson));");}
  else engine.Execute("globalThis.game = Object.freeze({inRaid:false,map:'',raidRemainingSeconds:0,fps:0,aliveAi:0,alivePlayers:0,hideoutCraftsReady:0,cultistReady:false});");
  var result=engine.Evaluate("(async function(){\n"+source+"\n})()");engine.Advanced.ProcessTasks();deadline.Token.ThrowIfCancellationRequested();var value=result.UnwrapIfPromise(TimeSpan.Zero);
  if(value.IsNull()||value.IsUndefined())value=engine.GetValue("cornerText").UnwrapIfPromise(TimeSpan.Zero);
  if(value.IsNull()||value.IsUndefined())throw new ArgumentException("请填写显示表达式，或使用 return、cornerText = value 设置左下角文字。");
  if(!value.IsString()&&!value.IsNumber()&&!value.IsBoolean())throw new ArgumentException("请返回文字、数字或布尔值；JSON 对象需先取字段或 JSON.stringify。");
  return value.ToString();
 }
}

// Keep script libraries inside the core payload so hot reload and installers need
// no shared/global DLL installation. Installed before any Jint method is JITted.
internal static class CornerScriptDependencies {
 static AssemblyLoadContext? context;
 internal static void Install(){context=AssemblyLoadContext.GetLoadContext(typeof(CornerScriptDependencies).Assembly)!;context.Resolving+=Resolve;}
 static Assembly? Resolve(AssemblyLoadContext owner,AssemblyName name){if(name.Name is not ("Jint" or "Acornima"))return null;using var stream=typeof(CornerScriptDependencies).Assembly.GetManifestResourceStream("LocalSpt5Esp.Script."+name.Name+".dll");return stream==null?null:owner.LoadFromStream(stream);}
 internal static void Shutdown(){if(context!=null)context.Resolving-=Resolve;context=null;CornerScriptWeb.Shutdown();}
}
