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
 Reject("for(let i=0;i<5;i++) httpGet('http://example.test'); return 'x';","per-refresh request budget",fetch:_=>"ok");
 Reject(new string(' ',16385),"oversized scripts are rejected");
 using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();Reject("return 42;","cancelled reload work cannot run",cancelled.Token);}
 using var portReservation=new TcpListener(IPAddress.Loopback,0);portReservation.Start();int port=((IPEndPoint)portReservation.LocalEndpoint).Port;portReservation.Stop();
 using var listener=new HttpListener();string url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(url);listener.Start();
 var server=Task.Run(async()=>{var request=await listener.GetContextAsync();byte[] body=System.Text.Encoding.UTF8.GetBytes("{\"version\":\"local API\"}");request.Response.ContentType="application/json";request.Response.ContentLength64=body.Length;await request.Response.OutputStream.WriteAsync(body);request.Response.Close();});
 Check(Run("return JSON.parse(httpGet('"+url+"')).version;")=="local API","real HTTP response is decoded and parsed");await server;
 var large=Task.Run(async()=>{var request=await listener.GetContextAsync();request.Response.ContentLength64=300000;try{await request.Response.OutputStream.WriteAsync(new byte[300000]);}catch(HttpListenerException){}finally{request.Response.Close();}});
 Reject("return httpGet('"+url+"');","oversized HTTP response is rejected");await large;
 var hanging=Task.Run(async()=>{var request=await listener.GetContextAsync();await Task.Delay(500);request.Response.Close();});using var timeout=new CancellationTokenSource(100);var clock=Stopwatch.StartNew();Reject("return httpGet('"+url+"');","HTTP request obeys cancellation",timeout.Token);Check(clock.Elapsed<TimeSpan.FromSeconds(2),"cancellation returns promptly");await hanging;
 Console.WriteLine($"Passed {checks} script and HTTP checks.");
}finally{resolver.GetMethod("Shutdown",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);context.Unload();}
