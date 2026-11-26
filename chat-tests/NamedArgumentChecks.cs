using LocalSpt5Esp;
using System.Text.Json;
internal static class NamedArgumentChecks {
 internal static void Run(){
  static void Check(bool ok,string why){if(!ok)throw new Exception(why);Console.WriteLine("PASS "+why);}
  static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
  static (string[] Args,JsonElement Options) Read(string command,string text){var args=ChatCommandSyntax.Tokens(text);var o=ChatNamedArguments.Take(command,ref args);ChatCommandSyntax.ValidateArguments(command,args);ChatCommandOptions.Validate(command,args,o);return(args,o!.Value);}
  var give=Read("give","@s salewa 1 fir=true medical=400");Check(give.Args.SequenceEqual(new[]{"@s","salewa","1"})&&give.Options.GetProperty("foundInRaid").GetBoolean()&&give.Options.GetProperty("medicalResource").GetDouble()==400,"named item aliases preserve positional arguments and produce typed parameters");
  var ai=Read("ai","configure @e[type=scav,name=Big Pipe] name=\"中文 Test AI\" health=1000 pose=crouched playerRelation=friendly");
  Check(ai.Args.Length==2&&ai.Options.GetProperty("name").GetString()=="中文 Test AI"&&ai.Options.GetProperty("playerRelation").GetInt32()==1&&ai.Options.GetProperty("scene").GetProperty("pose").GetString()=="crouched","named actors accept quoted names, selectors, relations and nested scene aliases");
  var summon=Read("summon","assault count=2 pos=~,~,~5 difficulty=hard health=1000");
  Check(summon.Args.SequenceEqual(new[]{"assault","~","~","~5","2","2"})&&summon.Options.GetProperty("totalHealth").GetInt32()==1000,"named summon coordinates, count and difficulty normalize together");
  var defaults=Read("summon","assault count=2");Check(defaults.Args.SequenceEqual(new[]{"assault","~","~","~3","2","1"}),"named summon defaults to three meters ahead and normal difficulty");
  var weapon=Read("item","modify entity @s FirstPrimaryWeapon ergonomics=80 weapon.values.WeaponRecoilSettings.Test=2");
  Check(weapon.Options.GetProperty("weapon").GetProperty("values").GetProperty("Ergonomics").GetInt32()==80&&weapon.Options.GetProperty("weapon").GetProperty("values").GetProperty("WeaponRecoilSettings.Test").GetInt32()==2,"native weapon dotted field names remain one property inside values");
  var body=Read("ai","configure @e[type=ai] bodyHealth.Chest.maximum=300 bodyHealth.Chest.current=200");Check(body.Options.GetProperty("bodyHealth").GetProperty("Chest").GetProperty("current").GetInt32()==200,"explicit body health paths need no nested JSON syntax");
  foreach(string input in new[]{"@s salewa fir=true foundInRaid=false","@s salewa medical=NaN","@s salewa fir=maybe","@s salewa typo=1","@s salewa fir=true 2","@s salewa fir=true {}"})Check(Reject(()=>Read("give",input)),"invalid or ambiguous named item input is rejected: "+input);
  foreach(string input in new[]{"assault count=1.5","assault pos=~,~","assault ~ ~ ~ count=2","assault difficulty=nightmare"})Check(Reject(()=>Read("summon",input)),"invalid named summon does not fall back to another meaning: "+input);
  Check(Reject(()=>Read("ai","pause @e[type=ai] invincible=true")),"unsupported actor subcommands reject named options");
  string prefix="/give @s salewa ";var hints=ChatNamedArguments.Complete(prefix,prefix.Length,"give",new[]{"@s","salewa"},"");Check(hints.Any(h=>h.Text.EndsWith("fir=")&&h.Label.Contains("带钩")&&h.Detail.Contains("搜刮")),"named completion pairs short syntax with a readable explanation");
  prefix="/give @s salewa fir=";var vals=ChatNamedArguments.Complete(prefix,prefix.LastIndexOf(' ')+1,"give",new[]{"@s","salewa","fir="},"fir=");Check(vals.Select(v=>v.Text).SequenceEqual(new[]{"/give @s salewa fir=true","/give @s salewa fir=false"}),"boolean values complete after the equals sign");
  prefix="/give @s salewa fir=true ";hints=ChatNamedArguments.Complete(prefix,prefix.Length,"give",new[]{"@s","salewa","fir=true"},"");Check(hints.All(h=>!h.Text.EndsWith("fir=")),"already supplied named arguments are not suggested again");
  prefix="/summon assault di";hints=ChatNamedArguments.Complete(prefix,prefix.LastIndexOf(' ')+1,"summon",new[]{"assault","di"},"di");Check(hints.Any(h=>h.Text.EndsWith("difficulty=")),"partial argument names do not become accidental positional arguments");
  string chips="/ai configure @e[type=scav,name=Big Pipe] name=\"A B\" paused=true";var parsed=ChatNamedArguments.Chips(chips);Check(parsed.Length==2&&parsed[0].Value=="A B"&&chips[parsed[1].Start..parsed[1].End]=="paused=true","editable chips track full quoted token spans and exclude selector filters");
  Check(ChatPlaceHints.Kind("exit:abc123")=="撤离点"&&ChatPlaceHints.Kind("room:abc").Contains("房间")&&ChatPlaceHints.Description("edit").Contains("可视化"),"place tags and editor actions have human-readable descriptions");
  var preview=ChatCommandHints.For("/give @s salewa fir=",20);Check(preview.Current.Contains("带钩"),"named parameter preview explains the current field");
 }
}
