using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Diagnostics;
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
string root=Environment.GetEnvironmentVariable("SPT_SOURCE_ROOT")??Directory.GetCurrentDirectory();
var context=new AssemblyLoadContext("corner-script-test",isCollectible:true);
var core=context.LoadFromAssemblyPath(Path.Combine(root,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
var resolver=core.GetType("LocalSpt5Esp.CornerScriptDependencies",true)!;
resolver.GetMethod("Install",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
var evaluate=core.GetType("LocalSpt5Esp.CornerScript",true)!.GetMethod("Evaluate",BindingFlags.Static|BindingFlags.NonPublic)!;
string Run(string script,CancellationToken token=default,Func<string,string>? fetch=null){try{return (string)evaluate.Invoke(null,[script,token,fetch])!;}catch(TargetInvocationException e){throw e.InnerException!;}}
void Reject(string script,string label,CancellationToken token=default,Func<string,string>? fetch=null){bool rejected=false;try{Run(script,token,fetch);}catch{rejected=true;}Check(rejected,label);}
try{
 Check(Run("return 'SPT · ' + (21 * 2);")=="SPT · 42","script returns computed text");
 Check(context.Assemblies.Any(a=>a.GetName().Name=="Jint")&&context.Assemblies.Any(a=>a.GetName().Name=="Acornima"),"payload resolves both embedded libraries in its own load context");
 Check(Run("const x = JSON.parse(httpGet('http://example.test/api')); return x.version;",fetch:_=>"{\"version\":\"test-48558\"}")=="test-48558","JSON API fields become display text");
 Reject("while (true) {}","infinite loops stop within the statement budget");
 Reject("function f(){return f();} return f();","recursive scripts stop at the recursion limit");
 Reject("return System.IO.File.ReadAllText('anything');","script has no CLR or filesystem access");
 Reject("return httpGet('file:///C:/Windows/win.ini');","network helper rejects non-HTTP URLs");
 Reject("const x = 1;","missing return is reported");
 Reject("return { version: 'x' };","object results require explicit field selection or serialization");
 Reject("for(let i=0;i<65;i++) httpGet('http://example.test'); return 'x';","per-refresh request budget",fetch:_=>"ok");
 Reject(new string(' ',16385),"oversized scripts are rejected");
 using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();Reject("return 42;","cancelled reload work cannot run",cancelled.Token);}
 Check(Run("return await Promise.resolve('async result');")=="async result","top-level await and resolved Promise return values");
 Check(Run("async function name(){return 'Alice';} return name();")=="Alice","returned async function is awaited");
 Reject("return Promise.reject(new Error('expected failure'));","rejected Promise is reported");
 var pendingClock=Stopwatch.StartNew();Reject("return new Promise(() => {});","unsettled Promise is rejected without blocking reload");Check(pendingClock.Elapsed<TimeSpan.FromSeconds(1),"unsettled Promise returns promptly");
 Check(Run("const u = new URL('/students', 'https://example.test/base/'); u.search = new URLSearchParams({name: 'Alice & Bob', page: 2}); u.searchParams.append('tag','a'); u.searchParams.append('tag','b'); u.searchParams.set('page','3'); return u.pathname + '|' + u.searchParams.get('name') + '|' + u.searchParams.getAll('tag').join(',') + '|' + u.searchParams.get('page');")=="/students|Alice & Bob|a,b|3","URL, relative base and live URLSearchParams updates");
 Check(Run("return new URLSearchParams({q: 'a b+~!'}).toString();")=="q=a+b%2B%7E%21","query strings use form encoding");
 Check(Run("const response = await fetch(new URL('https://example.test/api')); const copy = response.clone(); const json = await response.json(); return response.ok + '|' + response.status + '|' + json.name + '|' + (await copy.json()).name;",fetch:_=>"{\"name\":\"Alice\"}")=="true|200|Alice|Alice","fetch response, JSON and clone work with await");
 Reject("const response = await fetch('https://example.test'); await response.text(); return await response.text();","response bodies can only be consumed once",fetch:_=>"ok");
 Reject("return await fetch('file:///C:/Windows/win.ini');","fetch rejects non-HTTP URLs");
 var intervalType=core.GetType("LocalSpt5Esp.CornerScriptInterval",true)!;var unitType=core.GetType("LocalSpt5Esp.CornerScriptTimeUnit",true)!;
 var parse=intervalType.GetMethod("TrySeconds",BindingFlags.Static|BindingFlags.NonPublic)!;var format=intervalType.GetMethod("Format",BindingFlags.Static|BindingFlags.NonPublic)!;
 foreach(string unit in new[]{"Hours","Minutes","Seconds","Milliseconds"})foreach(double seconds in new[]{.001,.1,30,3600}){object value=Enum.Parse(unitType,unit);string display=(string)format.Invoke(null,[seconds,value])!;object?[] parameters=[display,value,0d];bool valid=(bool)parse.Invoke(null,parameters)!;Check(valid&&Math.Abs((double)parameters[2]!-seconds)<1e-10,$"interval round trip {seconds}s via {unit}");}
 foreach(string input in new[]{"0","-1","NaN","Infinity","abc","3600001"}){object?[] parameters=[input,Enum.Parse(unitType,"Milliseconds"),0d];Check(!(bool)parse.Invoke(null,parameters)!,"invalid millisecond interval rejected: "+input);}
 string? savedConfig=Environment.GetEnvironmentVariable("SPT_CORNER_SCRIPT_CONFIG");
 if(savedConfig!=null){
  string raw=File.ReadLines(savedConfig).First(line=>line.StartsWith("Corner version script = ",StringComparison.Ordinal))["Corner version script = ".Length..];
  string saved=System.Text.RegularExpressions.Regex.Unescape(raw);int pages=0;
  string output=Run(saved,fetch:url=>{pages++;var uri=new Uri(url);Check(uri.Query.Contains("page="+pages)&&uri.Query.Contains("page_size=100")&&uri.Query.Contains("is_npc=false")&&uri.Query.Contains("is_skin=false"),"saved pagination script builds expected query "+pages);return System.Text.Json.JsonSerializer.Serialize(new{max_page=6,data=new{students=new[]{new{given_name_en="Alice",is_npc=false,is_skin=false},new{given_name_en="Ignored",is_npc=true,is_skin=false}}}});});
  Check(output=="Alice"&&pages==6,"actual saved async pagination script runs unchanged across six pages");
  if(Environment.GetEnvironmentVariable("SPT_CORNER_LIVE_REQUESTS")=="1"){
   string live=Run(saved);Check(!string.IsNullOrWhiteSpace(live),"actual saved script returns display text from its configured live API");
  }
 }
 using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;string url=$"http://127.0.0.1:{port}/";
 async Task Serve(int status,string body,Action<string,Dictionary<string,string>,string>? inspect=null,Dictionary<string,string>? replyHeaders=null,int? declaredSize=null,int delay=0){
  using var connection=await listener.AcceptTcpClientAsync();await using var stream=connection.GetStream();using var reader=new StreamReader(stream,System.Text.Encoding.UTF8,leaveOpen:true);
  string first=await reader.ReadLineAsync()??"";var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);string? line;
  while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync())){int split=line.IndexOf(':');if(split>0)headers[line[..split]]=line[(split+1)..].Trim();}
  int size=headers.TryGetValue("Content-Length",out var length)?int.Parse(length):0;char[] requestBody=new char[size];int read=0;
  while(read<size){int count=await reader.ReadAsync(requestBody.AsMemory(read));if(count==0)throw new EndOfStreamException();read+=count;}
  inspect?.Invoke(first,headers,new string(requestBody));if(delay>0)await Task.Delay(delay);
  byte[] bytes=System.Text.Encoding.UTF8.GetBytes(body);string extra=replyHeaders==null?"":string.Concat(replyHeaders.Select(p=>p.Key+": "+p.Value+"\r\n"));
  byte[] head=System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status==200?"OK":"Not Found")}\r\nContent-Length: {declaredSize??bytes.Length}\r\nConnection: close\r\n"+extra+"\r\n");
  try{await stream.WriteAsync(head);if(bytes.Length>0)await stream.WriteAsync(bytes);}catch(IOException)when(delay>0||declaredSize.HasValue){}
 }
 var server=Serve(200,"{\"version\":\"local API\"}");
 Check(Run("return JSON.parse(httpGet('"+url+"')).version;")=="local API","real HTTP response is decoded and parsed");await server;
 var notFound=Serve(404,"");Check(Run("const r = await fetch('"+url+"'); return r.ok + '|' + r.status;")=="false|404","fetch exposes HTTP errors through response status");await notFound;
 var post=Serve(200,"accepted",(request,headers,body)=>Check(request.StartsWith("POST ")&&headers["X-Test"]=="value"&&body=="{\"value\":42}","fetch sends method, custom headers and body"),new(){{"X-Reply","present"}});Check(Run("const r = await fetch('"+url+"', {method:'POST',headers:{'X-Test':'value','Content-Type':'application/json'},body:JSON.stringify({value:42})}); return (await r.text()) + '|' + r.headers.get('x-reply');")=="accepted|present","response text and case-insensitive headers");await post;
 var getWithContentType=Serve(200,"ok",(request,headers,body)=>Check(request.StartsWith("GET ")&&!headers.ContainsKey("Content-Length")&&body=="","GET request with Content-Type header does not attach empty body"));Check(Run("const r = await fetch('"+url+"', {method:'GET',headers:{'Content-Type':'application/json'}}); return await r.text();")=="ok","GET request with content header succeeds cleanly");await getWithContentType;
 var large=Serve(200,"",declaredSize:1048577);
 Reject("return httpGet('"+url+"');","oversized HTTP response is rejected");await large;
 var hanging=Serve(200,"",delay:500);using var timeout=new CancellationTokenSource(100);var clock=Stopwatch.StartNew();Reject("return httpGet('"+url+"');","HTTP request obeys cancellation",timeout.Token);Check(clock.Elapsed<TimeSpan.FromSeconds(2),"cancellation returns promptly");await hanging;
 var evalGame=core.GetType("LocalSpt5Esp.CornerScript",true)!.GetMethod("EvaluateWithGame",BindingFlags.Static|BindingFlags.NonPublic)!;
 string RunWithGame(string code,string? gameJson)=>(string)evalGame.Invoke(null,[code,CancellationToken.None,(Func<string,string>)(_=>"{}"),gameJson])!;
 Check(RunWithGame("return game.map + ':' + game.aliveAi + ':' + game.fps + ':' + game.cultistReady;", """{"inRaid":true,"map":"woods","raidRemainingSeconds":420,"fps":88.5,"aliveAi":7,"alivePlayers":8,"hideoutCraftsReady":2,"cultistReady":true}""")=="woods:7:88.5:true","globalThis.game exposes injected snapshot");
 Check(RunWithGame("'use strict'; try { game.map = 'hack'; return 'mutated'; } catch { return 'frozen'; }", """{"map":"customs"}""")=="frozen","globalThis.game is frozen");
 Check(Run("return [game.state, game.raid, game.player, game.hideout.craftsReady, game.performance, game.raidRemainingSeconds].map(JSON.stringify).join('|');")==
  "\"menu\"|null|null|0|null|0","empty snapshot has the full top-level shape and legacy defaults");
 const string raidSnapshot="""{"state":"raid","raid":{"mapName":"Woods","remainingSeconds":133.4,"population":{"ai":3},"exits":[{"name":"Outskirts","distanceMeters":42.5}]},"player":{"vitals":{"health":{"current":420}}},"fps":90}""";
 Check(RunWithGame("return game.raid.exits[0].name + '|' + game.raid.exits[0].distanceMeters + '|' + game.player.vitals.health.current;",raidSnapshot)=="Outskirts|42.5|420","scripts can combine nested raid, exit and player data");
 Check(RunWithGame("return [game, game.raid, game.raid.exits, game.raid.exits[0], game.player.vitals.health].every(Object.isFrozen);",raidSnapshot)=="true","nested snapshot objects and arrays are read-only");
 var gameExample=(string)core.GetType("LocalSpt5Esp.CornerScript",true)!.GetField("GameExample",BindingFlags.Static|BindingFlags.NonPublic)!.GetRawConstantValue()!;
 Check(Run(gameExample)=="Crafts: 0 · FPS: 0"&&RunWithGame(gameExample,raidSnapshot)=="Woods · 134s · AI:3 · HP:420","built-in raid example works in the menu and raid");
 Check(Run("console.log('ignored', {a:1}); return 'ok';")=="ok","console.log is safe during regular evaluation");
 var evaluateDisplay=core.GetType("LocalSpt5Esp.CornerScript",true)!.GetMethod("EvaluateDisplay",BindingFlags.Static|BindingFlags.NonPublic)!;
 string RunDisplay(string code,string expression,string? gameJson=null)=>(string)evaluateDisplay.Invoke(null,[code,expression,CancellationToken.None,null,gameJson])!;
 Check(RunDisplay("const stats = { fps: game.fps }; // last line","stats.fps + ' FPS'","""{"fps":88.5}""")=="88.5 FPS","display expression reads local variables and the game snapshot");
 Check(RunDisplay("","game.map","""{"map":"woods"}""")=="woods","display expression runs without a script body");
 Check(RunDisplay("const status = await Promise.resolve('ready');","status")=="ready","display expression reads awaited values");
 Check(RunDisplay("return 'legacy';","missingVariable")=="legacy","explicit return keeps its normal precedence");
 Check(Run("cornerText = 0;")=="0"&&Run("cornerText = false;")=="false"&&Run("return '';")=="","assigned and legacy falsy values remain valid");
 var export=core.GetType("LocalSpt5Esp.CornerScript",true)!.GetMethod("SourceWithExpression",BindingFlags.Static|BindingFlags.NonPublic)!;
 Check(Run((string)export.Invoke(null,["const status = 42;","status"])!)=="42","exported script includes its display expression");
 var evalTest=core.GetType("LocalSpt5Esp.CornerScript",true)!.GetMethod("EvaluateTest",BindingFlags.Static|BindingFlags.NonPublic)!;
 object RunTest(string code,string? gameJson=null)=>evalTest.Invoke(null,[code,CancellationToken.None,null,gameJson])!;
 T Field<T>(object target,string name)=>(T)target.GetType().GetProperty(name)!.GetValue(target)!;
 var okTest=RunTest("console.log('step', 1); console.warn('hint'); return game.map + ':' + 42;", """{"map":"reserve"}""");
 Check(Field<bool>(okTest,"Ok")&&Field<string>(okTest,"Value")=="reserve:42"&&Field<string[]>(okTest,"Logs").SequenceEqual(new[]{"[LOG] step 1","[WARN] hint"}),"EvaluateTest captures console logs and return value");
 var errTest=RunTest("console.info('before fail'); throw new Error('boom');");
 Check(!Field<bool>(errTest,"Ok")&&Field<string>(errTest,"Error").Contains("boom")&&Field<string[]>(errTest,"Logs").SequenceEqual(new[]{"[INFO] before fail"}),"EvaluateTest preserves logs and error details on failure");
 var displayTest=core.GetType("LocalSpt5Esp.CornerScript",true)!.GetMethod("EvaluateDisplayTest",BindingFlags.Static|BindingFlags.NonPublic)!;
 var withExpression=displayTest.Invoke(null,["const status = 42; console.log(status);","status",CancellationToken.None,null,null])!;
 Check(Field<bool>(withExpression,"Ok")&&Field<string>(withExpression,"Value")=="42"&&Field<string[]>(withExpression,"Logs").SequenceEqual(new[]{"[LOG] 42"}),"test runs use the same display expression and keep logs");
 Console.WriteLine($"Passed {checks} script and HTTP checks.");
}finally{resolver.GetMethod("Shutdown",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);context.Unload();}
