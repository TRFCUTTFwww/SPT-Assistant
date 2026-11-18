using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
namespace LocalSpt5Esp;

// Browser-style data APIs for the worker engine; no browser or CLR access.
internal static class CornerScriptWeb {
 internal sealed record Reply(int status,string statusText,string url,Dictionary<string,string> headers,string body);
 static readonly HttpClient client=new(){Timeout=System.Threading.Timeout.InfiniteTimeSpan};
 internal static void Shutdown()=>client.Dispose();
 internal static Reply Send(string url,string options,CancellationToken cancellation){
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo))throw new ArgumentException("请求需要不含用户名和密码的 HTTP 或 HTTPS 地址。");
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(8));
  using var settings=JsonDocument.Parse(options);var root=settings.RootElement;
  string method=root.TryGetProperty("method",out var m)?m.GetString()??"GET":"GET";
  using var request=new HttpRequestMessage(new HttpMethod(method),uri);
  if(root.TryGetProperty("body",out var body)&&body.ValueKind==JsonValueKind.String){string value=body.GetString()!;if(Encoding.UTF8.GetByteCount(value)>1048576)throw new InvalidOperationException("请求正文超过 1 MiB。");request.Content=new StringContent(value,Encoding.UTF8);}
  if(root.TryGetProperty("headers",out var headers)&&headers.ValueKind==JsonValueKind.Object)foreach(var pair in headers.EnumerateObject()){
   string? headerValue=pair.Value.ValueKind==JsonValueKind.String?pair.Value.GetString():pair.Value.ToString();
   if(!request.Headers.TryAddWithoutValidation(pair.Name,headerValue)){
    if(request.Content==null&&request.Method!=HttpMethod.Get&&request.Method!=HttpMethod.Head)request.Content=new ByteArrayContent(Array.Empty<byte>());
    if(request.Content!=null){request.Content.Headers.Remove(pair.Name);request.Content.Headers.TryAddWithoutValidation(pair.Name,headerValue);}
   }
  }
  using var response=client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token).GetAwaiter().GetResult();
  if(response.Content.Headers.ContentLength>1048576)throw new InvalidOperationException("API 响应超过 1 MiB。");
  using var input=response.Content.ReadAsStream(deadline.Token);using var output=new MemoryStream();byte[] buffer=new byte[8192];
  while(true){int count=input.ReadAsync(buffer.AsMemory(),deadline.Token).AsTask().GetAwaiter().GetResult();if(count==0)break;if(output.Length+count>1048576)throw new InvalidOperationException("API 响应超过 1 MiB。");output.Write(buffer,0,count);}
  var responseHeaders=response.Headers.Concat(response.Content.Headers).ToDictionary(p=>p.Key,p=>string.Join(", ",p.Value),StringComparer.OrdinalIgnoreCase);
  Encoding encoding=Encoding.UTF8;try{if(response.Content.Headers.ContentType?.CharSet is {} charset)encoding=Encoding.GetEncoding(charset.Trim('"'));}catch(ArgumentException){}
  return new((int)response.StatusCode,response.ReasonPhrase??"",response.RequestMessage?.RequestUri?.AbsoluteUri??url,responseHeaders,encoding.GetString(output.ToArray()));
 }
 internal static string ParseUrl(string input,string baseUrl){
  Uri uri=baseUrl.Length>0?new Uri(new Uri(baseUrl,UriKind.Absolute),input):new Uri(input,UriKind.Absolute);
  return Parts(uri);
 }
 static string Parts(Uri uri)=>JsonSerializer.Serialize(new{href=uri.AbsoluteUri,protocol=uri.Scheme+":",host=uri.Authority,hostname=uri.Host,port=uri.IsDefaultPort?"":uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),pathname=uri.AbsolutePath,search=uri.Query,hash=uri.Fragment,origin=uri.GetLeftPart(UriPartial.Authority)});
 internal static string UpdateUrl(string input,string part,string value){
  if(part=="href")return ParseUrl(value,"");var builder=new UriBuilder(input);
  switch(part){case "search":builder.Query=value.TrimStart('?');break;case "hash":builder.Fragment=value.TrimStart('#');break;case "pathname":builder.Path=value;break;case "protocol":builder.Scheme=value.TrimEnd(':');break;case "hostname":builder.Host=value;break;case "port":builder.Port=value.Length==0?-1:int.Parse(value,System.Globalization.CultureInfo.InvariantCulture);break;case "host":var host=new Uri(builder.Scheme+"://"+value);builder.Host=host.Host;builder.Port=host.IsDefaultPort?-1:host.Port;break;default:throw new ArgumentException("Unsupported URL property");}
  return Parts(builder.Uri);
 }
 internal const string Bootstrap="""
 (function(request, parseUrl, updateUrl, log) {
   const formatLog = args => args.map(value => {
     if (value === undefined) return 'undefined';
     if (value === null) return 'null';
     if (typeof value === 'string') return value;
     try { return JSON.stringify(value); } catch { return String(value); }
   }).join(' ');
   globalThis.console = {
     log: (...args) => log && log('LOG', formatLog(args)),
     info: (...args) => log && log('INFO', formatLog(args)),
     warn: (...args) => log && log('WARN', formatLog(args)),
     error: (...args) => log && log('ERROR', formatLog(args))
   };
   const decode = s => { try { return decodeURIComponent(s.replace(/\+/g, ' ')); } catch { return s; } };
   const encode = s => encodeURIComponent(String(s)).replace(/[!'()~]/g, c => '%' + c.charCodeAt(0).toString(16).toUpperCase()).replace(/%20/g, '+');
   class URLSearchParams {
     constructor(init = '') {
       this._pairs = []; this._change = null;
       if (typeof init === 'string') this._reset(init);
       else if (init != null && typeof init[Symbol.iterator] === 'function') {
         for (const pair of init) { const row = Array.from(pair); if (row.length !== 2) throw new TypeError('Expected a name/value pair'); this._pairs.push(row.map(String)); }
       } else if (init != null) for (const name of Object.keys(init)) this._pairs.push([String(name), String(init[name])]);
     }
     _reset(text) { this._pairs = String(text).replace(/^\?/, '').split('&').filter(Boolean).map(s => { const n = s.indexOf('='); return n < 0 ? [decode(s), ''] : [decode(s.slice(0, n)), decode(s.slice(n + 1))]; }); }
     _notify() { if (this._change) this._change(this.toString()); }
     append(name, value) { this._pairs.push([String(name), String(value)]); this._notify(); }
     set(name, value) { name = String(name); value = String(value); let found = false; this._pairs = this._pairs.filter(p => p[0] !== name || (!found && (p[1] = value, found = true))); if (!found) this._pairs.push([name, value]); this._notify(); }
     delete(name, value) { name = String(name); this._pairs = this._pairs.filter(p => p[0] !== name || (arguments.length > 1 && p[1] !== String(value))); this._notify(); }
     get(name) { const row = this._pairs.find(p => p[0] === String(name)); return row ? row[1] : null; }
     getAll(name) { return this._pairs.filter(p => p[0] === String(name)).map(p => p[1]); }
     has(name, value) { return this._pairs.some(p => p[0] === String(name) && (arguments.length < 2 || p[1] === String(value))); }
     sort() { this._pairs.sort((a, b) => a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0); this._notify(); }
     get size() { return this._pairs.length; }
     entries() { return this._pairs.map(p => p.slice())[Symbol.iterator](); }
     keys() { return this._pairs.map(p => p[0])[Symbol.iterator](); }
     values() { return this._pairs.map(p => p[1])[Symbol.iterator](); }
     forEach(fn, owner) { for (const [key, value] of this._pairs) fn.call(owner, value, key, this); }
     [Symbol.iterator]() { return this.entries(); }
     toString() { return this._pairs.map(p => encode(p[0]) + '=' + encode(p[1])).join('&'); }
   }
   class URL {
     constructor(input, base) { this._parts = JSON.parse(parseUrl(String(input), base === undefined ? '' : String(base))); this._params = null; }
     _set(name, value) { this._parts = JSON.parse(updateUrl(this.href, name, String(value))); if (this._params) this._params._reset(this.search); }
     get href() { return this._parts.href; } set href(v) { this._set('href', v); }
     get search() { return this._parts.search; } set search(v) { this._set('search', v); }
     get hash() { return this._parts.hash; } set hash(v) { this._set('hash', v); }
     get pathname() { return this._parts.pathname; } set pathname(v) { this._set('pathname', v); }
     get protocol() { return this._parts.protocol; } set protocol(v) { this._set('protocol', v); }
     get host() { return this._parts.host; } set host(v) { this._set('host', v); }
     get hostname() { return this._parts.hostname; } set hostname(v) { this._set('hostname', v); }
     get port() { return this._parts.port; } set port(v) { this._set('port', v); }
     get origin() { return this._parts.origin; }
     get searchParams() { if (!this._params) { this._params = new URLSearchParams(this.search); this._params._change = value => this._set('search', value); } return this._params; }
     toString() { return this.href; } toJSON() { return this.href; }
   }
   class Headers {
     constructor(init = {}) { this._values = new Map(); if (init != null && typeof init[Symbol.iterator] === 'function') { for (const [name, value] of init) this.append(name, value); } else for (const name of Object.keys(init || {})) this.append(name, init[name]); }
     append(name, value) { name = String(name).toLowerCase(); this._values.set(name, this.has(name) ? this.get(name) + ', ' + String(value) : String(value)); }
     set(name, value) { this._values.set(String(name).toLowerCase(), String(value)); }
     get(name) { return this._values.get(String(name).toLowerCase()) ?? null; }
     has(name) { return this._values.has(String(name).toLowerCase()); }
     delete(name) { this._values.delete(String(name).toLowerCase()); }
     entries() { return this._values.entries(); } keys() { return this._values.keys(); } values() { return this._values.values(); }
     forEach(fn, owner) { for (const [name, value] of this._values) fn.call(owner, value, name, this); }
     [Symbol.iterator]() { return this.entries(); }
   }
   class Response {
     constructor(body = '', init = {}) { this._body = String(body); this.status = init.status ?? 200; this.statusText = init.statusText ?? ''; this.url = init.url ?? ''; this.headers = new Headers(init.headers); this.ok = this.status >= 200 && this.status < 300; this.bodyUsed = false; }
     async text() { if (this.bodyUsed) throw new TypeError('Response body already used'); this.bodyUsed = true; return this._body; }
     async json() { return JSON.parse(await this.text()); }
     clone() { if (this.bodyUsed) throw new TypeError('Response body already used'); return new Response(this._body, this); }
   }
   async function fetch(input, init = {}) {
     const headers = new Headers(init.headers), options = { method: String(init.method || 'GET').toUpperCase(), headers: Object.fromEntries(headers) };
     if (init.body !== undefined && init.body !== null) { options.body = String(init.body); if (init.body instanceof URLSearchParams && !headers.has('content-type')) options.headers['content-type'] = 'application/x-www-form-urlencoded;charset=UTF-8'; }
     const result = JSON.parse(request(String(input), JSON.stringify(options))); return new Response(result.body, result);
   }
   globalThis.URLSearchParams = URLSearchParams; globalThis.URL = URL; globalThis.Headers = Headers; globalThis.Response = Response; globalThis.fetch = fetch;
   globalThis.httpGet = url => { const result = JSON.parse(request(String(url), '{}')); if (result.status < 200 || result.status >= 300) throw new Error('HTTP ' + result.status + ' ' + result.statusText); return result.body; };
 })(__cornerRequest, __cornerParseUrl, __cornerUpdateUrl, typeof __cornerLog === 'function' ? __cornerLog : null);
 delete globalThis.__cornerRequest; delete globalThis.__cornerParseUrl; delete globalThis.__cornerUpdateUrl; delete globalThis.__cornerLog;
 """;
}
