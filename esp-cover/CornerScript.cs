using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
namespace LocalSpt5Esp;

// Runs only managed code on a worker. No Unity objects or CLR access are exposed.
internal static class CornerScript {
 internal const string Example="// 返回要显示的文字。联网示例：\n// const data = JSON.parse(httpGet(\"https://example.com/api\"));\n// return data.version;\nreturn \"SPT · \" + new Date().toLocaleTimeString();";
 internal static string Evaluate(string source,CancellationToken cancellation,Func<string,string>? fetch=null){
  if(source.Length>16384)throw new ArgumentException("脚本最多 16384 个字符。");
  int calls=0;
  using var engine=new Engine(options=>options.MaxStatements(20000).LimitRecursion(32).LimitMemory(8*1024*1024).TimeoutInterval(TimeSpan.FromSeconds(5)).CancellationToken(cancellation));
  engine.SetValue("httpGet",new Func<string,string>(url=>{cancellation.ThrowIfCancellationRequested();if(++calls>4)throw new InvalidOperationException("每次刷新最多 4 个 HTTP 请求。");return (fetch??(value=>Fetch(value,cancellation)))(url);}));
  var value=engine.Evaluate("(function(){\n"+source+"\n})()");
  if(value.IsNull()||value.IsUndefined())throw new ArgumentException("请使用 return 返回要显示的文字。");
  if(!value.IsString()&&!value.IsNumber()&&!value.IsBoolean())throw new ArgumentException("请返回文字、数字或布尔值；JSON 对象需先取字段或 JSON.stringify。");
  return value.ToString();
 }
 static string Fetch(string url,CancellationToken cancellation){
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo))throw new ArgumentException("httpGet 需要 HTTP 或 HTTPS 地址。");
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(3));
  using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};
  using var response=client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,deadline.Token).GetAwaiter().GetResult();response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>262144)throw new InvalidOperationException("API 响应超过 256 KiB。");
  using var input=response.Content.ReadAsStream(deadline.Token);using var output=new MemoryStream();byte[] buffer=new byte[8192];
  while(true){int count=input.ReadAsync(buffer.AsMemory(),deadline.Token).AsTask().GetAwaiter().GetResult();if(count==0)break;if(output.Length+count>262144)throw new InvalidOperationException("API 响应超过 256 KiB。");output.Write(buffer,0,count);}
  return Encoding.UTF8.GetString(output.ToArray());
 }
}

// Keep script libraries inside the core payload so hot reload and installers need
// no shared/global DLL installation. Installed before any Jint method is JITted.
internal static class CornerScriptDependencies {
 static AssemblyLoadContext? context;
 internal static void Install(){context=AssemblyLoadContext.GetLoadContext(typeof(CornerScriptDependencies).Assembly)!;context.Resolving+=Resolve;}
 static Assembly? Resolve(AssemblyLoadContext owner,AssemblyName name){if(name.Name is not ("Jint" or "Acornima"))return null;using var stream=typeof(CornerScriptDependencies).Assembly.GetManifestResourceStream("LocalSpt5Esp.Script."+name.Name+".dll");return stream==null?null:owner.LoadFromStream(stream);}
 internal static void Shutdown(){if(context!=null)context.Resolving-=Resolve;context=null;}
}
