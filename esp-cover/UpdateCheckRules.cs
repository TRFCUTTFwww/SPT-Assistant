using System;
using System.Text.RegularExpressions;
namespace LocalSpt5Esp;
internal static class UpdateCheckRules {
 internal const string Repository="TRFCUTTFwww/SPT-Assistant";
 internal const string Endpoint="https://api.github.com/repos/"+Repository+"/releases/latest";
 internal const string Releases="https://github.com/"+Repository+"/releases/latest";
 internal static Version Parse(string tag){
  var match=Regex.Match(tag.Trim(),@"^[vV]?(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?$");
  if(!match.Success)throw new ArgumentException("不支持的正式版本号。");
  int Part(int i)=>match.Groups[i].Success?int.Parse(match.Groups[i].Value):0;
  return new Version(Part(1),Part(2),Part(3),Part(4));
 }
 internal static Uri Proxy(string address){
  if(!Uri.TryCreate(address.Trim(),UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https" or "socks5")||string.IsNullOrEmpty(uri.Host)||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.Fragment.Length>0)throw new ArgumentException("代理格式：http://127.0.0.1:7890 或 socks5://127.0.0.1:1080");
  return uri;
 }
 internal static bool IsNewer(string current,string remote)=>Parse(remote)>Parse(current);
}
