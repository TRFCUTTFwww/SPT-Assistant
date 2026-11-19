using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using Jint;
namespace LocalSpt5Esp;

// Only the Unity-thread entry points pass live object references.
internal static class CornerScript {
 internal sealed record TestRunResult(bool Ok,string Value,string Error,string[] Logs);
 internal sealed record LiveObjects(object? World,object? Player,object? Profile);
 internal const string LiveDirective="// @game-thread";
 internal const string LiveExample=LiveDirective+"\nreturn player?.Profile?.Nickname ?? '当前没有战局角色';";
 internal static bool UsesGameThread(string source){
  var firstLine=source.AsSpan().TrimStart();
  int end=firstLine.IndexOfAny('\r','\n');
  return (end<0?firstLine:firstLine[..end]).TrimEnd().SequenceEqual(LiveDirective);
 }
 internal const string Example="return \"SPT · \" + new Date().toLocaleTimeString();";
 internal const string AsyncExample="const url = new URL(\"https://example.com/api/status\");\nurl.search = new URLSearchParams({ page: 1 });\nconst response = await fetch(url);\nif (!response.ok) throw new Error(\"HTTP \" + response.status);\nconst data = await response.json();\nreturn data.version;";
 internal const string GameExample="if (!game.raid) return `Crafts: ${game.hideout.craftsReady} · FPS: ${game.fps}`;\nconst { mapName, remainingSeconds, population } = game.raid;\nconst hp = game.player?.vitals?.health?.current ?? '—';\nreturn `${mapName} · ${Math.ceil(remainingSeconds ?? 0)}s · AI:${population?.ai ?? '?'} · HP:${hp}`;";
 internal const string VariableExample="const status = game.inRaid ? game.map + \" · AI:\" + game.aliveAi : \"Hideout · FPS:\" + game.fps;";
 internal static string Evaluate(string source,CancellationToken cancellation,Func<string,string>? fetch=null)=>EvaluateWithGame(source,cancellation,fetch,null);
 internal static string EvaluateWithGame(string source,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson)=>EvaluateDisplay(source,"",cancellation,fetch,gameJson);
 internal static string EvaluateDisplay(string source,string expression,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson)=>ExecuteCore(source,expression,cancellation,fetch,gameJson,null);
 internal static TestRunResult EvaluateTest(string source,CancellationToken cancellation,Func<string,string>? fetch=null,string? gameJson=null)=>EvaluateDisplayTest(source,"",cancellation,fetch,gameJson);
 internal static TestRunResult EvaluateDisplayTest(string source,string expression,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson)=>RunTest(source,expression,cancellation,fetch,gameJson,null);
 internal static TestRunResult RunTest(string source,string expression,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson,LiveObjects? live){
  var logs=new List<string>();
  void Capture(string level,string message){if(logs.Count<200)logs.Add($"[{level}] {message}");else if(logs.Count==200)logs.Add("[LOG] (日志已达 200 行上限，后续已省略)");}
  try{
   string value=ExecuteCore(source,expression,cancellation,fetch,gameJson,Capture,live);
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
 internal static string ExecuteCore(string source,string expression,CancellationToken cancellation,Func<string,string>? fetch,string? gameJson,Action<string,string>? log,LiveObjects? live=null){
  source=SourceWithExpression(source,expression);
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);var budget=live==null?TimeSpan.FromSeconds(30):TimeSpan.FromMilliseconds(150);deadline.CancelAfter(budget);int calls=0;
  using var engine=new Engine(options=>options.MaxStatements(200000).LimitRecursion(64).LimitMemory(32*1024*1024).TimeoutInterval(budget).CancellationToken(deadline.Token));
  if(live!=null){
   engine.SetValue("world",live.World);
   engine.SetValue("player",live.Player);
   engine.SetValue("profile",live.Profile);
  }
  engine.SetValue("__cornerRequest",new Func<string,string,string>((url,options)=>{if(live!=null)throw new InvalidOperationException("主线程脚本无法使用 HTTP，请在后台脚本中请求网络。");deadline.Token.ThrowIfCancellationRequested();if(++calls>64)throw new InvalidOperationException("每次刷新最多 64 个 HTTP 请求。");var reply=fetch!=null?new CornerScriptWeb.Reply(200,"OK",url,new(),fetch(url)):CornerScriptWeb.Send(url,options,deadline.Token);return JsonSerializer.Serialize(reply);}));
  engine.SetValue("__cornerParseUrl",new Func<string,string,string>(CornerScriptWeb.ParseUrl));engine.SetValue("__cornerUpdateUrl",new Func<string,string,string,string>(CornerScriptWeb.UpdateUrl));
  if(log!=null)engine.SetValue("__cornerLog",log);
  engine.Execute(CornerScriptWeb.Bootstrap+"\nglobalThis.cornerText = undefined;");
  engine.SetValue("__cornerGameJson",string.IsNullOrWhiteSpace(gameJson)?CornerGameData.EmptyJson:gameJson);
  engine.Execute("""
   (() => {
    const freeze = value => {
     if (value !== null && typeof value === 'object') {
      for (const key of Object.keys(value)) freeze(value[key]);
      Object.freeze(value);
     }
     return value;
    };
    globalThis.game = freeze(JSON.parse(__cornerGameJson));
    globalThis.client = game.client ?? Object.freeze({serverUrl:null,version:null});
   })();
   """);
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
