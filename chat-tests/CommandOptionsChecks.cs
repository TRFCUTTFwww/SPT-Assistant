using System.Text.Json;
using LocalSpt5Esp;
internal static class CommandOptionsChecks {
 internal static void Run(){
  static void Check(bool value,string reason){if(!value)throw new Exception(reason);Console.WriteLine("PASS "+reason);}
  static bool Reject(Action action){try{action();return false;}catch(Exception e) when(e is ArgumentException or JsonException){return true;}}
  const string config="""{"name":"中文 {AI} \"A\" \\ room","scene":{"look":true},"effects":[{"type":"PainKiller","seconds":120}]}""";
  var tokens=ChatCommandSyntax.Tokens("ai configure @e[type=scav,name=Big Pipe] "+config);
  Check(tokens.Length==4&&tokens[^1]==config,"nested JSON preserves unicode, selector spaces, quotes and backslashes");
  var positional=tokens.Skip(1).ToArray();var options=ChatCommandOptions.Take(ref positional);
  Check(positional.SequenceEqual(new[]{"configure","@e[type=scav,name=Big Pipe]"})&&options!.Value.GetProperty("name").GetString()=="中文 {AI} \"A\" \\ room","final JSON is detached from positional arguments without changing string values");
  ChatCommandSyntax.ValidateArguments("ai",positional);ChatCommandOptions.Validate("ai",positional,options);
  Check(true,"detailed AI options validate on the configure route");
  foreach(var command in new[]{("item",new[]{"inspect","entity","@s","Backpack"}),("loot",new[]{"give","@e[type=ai]","loadout:test"}),("ai",new[]{"pause","@e[type=ai]"})})
   Check(Reject(()=>ChatCommandOptions.Validate(command.Item1,command.Item2,JsonDocument.Parse("{}").RootElement)),"unsupported subcommands reject JSON instead of ignoring it: "+command.Item1);
  Check(Reject(()=>ChatCommandSyntax.ValidateArguments("item",new[]{"inspect","typo","@s","Backpack"}))&&Reject(()=>ChatCommandSyntax.ValidateArguments("item",new[]{"replace","entity","@s","Backpack","typo","bag"})),"extended item routes validate entity and with keywords before mutations");
  Check(Reject(()=>ChatCommandSyntax.Tokens("give @s bag {}extra"))&&Reject(()=>ChatCommandSyntax.Tokens("give @s bag {\"contents\":[")),"adjacent suffixes and unclosed JSON are rejected");
  foreach(var json in new[]{"{\"count\":1,\"count\":2}","{\"typo\":1}","[]"})
   Check(Reject(()=>ChatCommandOptions.Keys(JsonDocument.Parse(json).RootElement,"count")),"duplicate, unknown or non-object options fail closed: "+json);
  using var values=JsonDocument.Parse("""{"count":2.5,"flag":"true","limit":1e300,"zero":0,"yes":true}""");
  Check(Reject(()=>ChatCommandOptions.Number(values.RootElement,"count",1,100,true))&&Reject(()=>ChatCommandOptions.Bool(values.RootElement,"flag"))&&Reject(()=>ChatCommandOptions.Number(values.RootElement,"limit",0,float.MaxValue)),"fractional counts, string booleans and out-of-storage-range values are rejected");
  Check(ChatCommandOptions.Number(values.RootElement,"zero",0,100)==0&&ChatCommandOptions.Bool(values.RootElement,"yes"),"valid zero and boolean values remain accepted");
  Check(ChatCommandOptions.CanAppend("give",new[]{"@s","bag"})&&!ChatCommandOptions.CanAppend("give",new[]{"@s"})&&!ChatCommandOptions.CanAppend("summon",new[]{"assault","~"}),"JSON starters appear only after valid positional arguments");
  Check(!ChatCommandOptions.KeysFor("item",new[]{"modify"}).Contains("attachments")&&ChatCommandOptions.KeysFor("item",new[]{"replace"}).Contains("attachments"),"existing item edits distinguish component changes from detached structure construction");
  string text="/ai configure @e[type=ai] "+config+" suffix";int start=text.IndexOf('{',text.IndexOf("]",StringComparison.Ordinal));
  Check(ChatCommandSyntax.CompletionStart(text[..(start+20)])==start&&ChatCommandSyntax.CompletionEnd(text,start+20)==text.Length-7,"JSON completion replaces the entire nested token and preserves later arguments");
  string prefix="/give @s bag {\"foundInRaid\":false}";var args=new[]{"@s","bag","{\"foundInRaid\":false}"};
  var suggestions=RaidCommands.OptionSuggestions(prefix,prefix.IndexOf('{'),"give",args,args[^1]);
  Check(suggestions.Length>0&&suggestions.All(s=>{var a=ChatCommandSyntax.Tokens(s.Text[1..]);var o=ChatCommandOptions.Take(ref a);return o!.Value.GetProperty("foundInRaid").ValueKind==JsonValueKind.False;}),"adding a top-level JSON field preserves all previous configuration");
  foreach(var broken in new[]{"{\"foundInRaid\":f","{\"foundInRaid\":true,\"foundInRaid\":false}","{\"unknown\":1}"})
   Check(RaidCommands.OptionSuggestions("/give @s bag "+broken,13,"give",new[]{"@s","bag",broken},broken).Length==0,"completion never replaces malformed or ambiguous existing configuration: "+broken);
  Check(RaidCommands.OptionSuggestions("/give @s bag {",13,"give",new[]{"@s","bag","{"},"{").Length>0,"an empty opening brace offers editable JSON templates");
  var hint=ChatCommandHints.For("/item modify entity @s Backpack {}",33);
  Check(hint.Current.Contains("foundInRaid")&&!hint.Current.Contains("attachments"),"JSON hints show fields actually supported by the selected subcommand");
  var completion=new ChatCompletion();string big="/give @s bag {\"name\":\""+new string('a',2500)+"\"}";
  var result=completion.Cycle("/give @s bag {",13,false,_=>new[]{new ChatSuggestion(big,"details")});
  Check(result.Text==big&&result.Cursor==big.Length,"local detailed commands can complete beyond the chat transport's 2000-character limit");
 }
}
