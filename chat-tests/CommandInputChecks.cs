using LocalSpt5Esp;

internal static class CommandInputChecks {
 internal static void Run(){
  static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
  var completion=new ChatCompletion();int calls=0;
  ChatSuggestion[] Suggest(string prefix){calls++;return (prefix switch{"/t"=>new[]{"/tag","/teleport","/time","/tp"},"/time s"=>new[]{"/time set"},"/tp \"boss:Big"=>new[]{"/tp \"boss:Big Pipe\""},_=>Array.Empty<string>()}).Select(s=>new ChatSuggestion(s,s)).ToArray();}
  completion.Refresh("/t",2,Suggest);Check(completion.Candidates.Length==4&&completion.Selected==-1,"typing exposes candidates before Tab");
  var result=completion.Cycle("/t",2,false,Suggest);Check(result==("/tag",4),"Tab completes with a collapsed caret at the inserted end");
  result=completion.Cycle(result.Text,result.Cursor,false,Suggest);Check(result==("/teleport",9)&&calls==1,"successive Tab cycles the original prefix without rescanning");
  result=completion.Cycle(result.Text,result.Cursor,true,Suggest);Check(result==("/tag",4),"Shift+Tab reverses completion");
  result=completion.Cycle(result.Text,2,true,Suggest);Check(result==("/tp",3)&&calls==2,"moving the caret invalidates the cycle and replaces the full token");
  result=completion.Cycle("/time st noon",7,false,Suggest);Check(result==("/time set noon",9),"mid-token completion preserves later arguments without duplicated suffix");
  result=completion.Cycle("/tp \"boss:Big P\" ~ ~1 ~","/tp \"boss:Big".Length,false,Suggest);Check(result==("/tp \"boss:Big Pipe\" ~ ~1 ~","/tp \"boss:Big Pipe\"".Length),"quoted entity completion replaces its complete token and preserves coordinates");
  completion.Refresh("plain text",10,Suggest);Check(completion.Candidates.Length==0,"normal chat clears stale candidates");
  completion.Refresh("//time",6,Suggest);Check(completion.Candidates.Length==0,"escaped slash messages have no command suggestions");
  completion.Refresh("/t",2,Suggest);Check(completion.MoveSelection(1)&&completion.Selected==0&&completion.Pending,"Down selects the first suggestion without changing input");
  completion.MoveSelection(1);result=completion.Cycle("/t",2,false,Suggest);Check(result==("/teleport",9)&&!completion.Pending,"Tab accepts the arrow-selected entry without skipping it");
  completion.MoveSelection(-1);result=completion.Accept(result.Text,result.Cursor);Check(result==("/tag",4)&&!completion.Pending,"Enter acceptance fills one suggestion without submitting a command");
  completion.Refresh("/t",2,Suggest);completion.MoveSelection(-1);Check(completion.Selected==3,"Up selects the last entry and selection wraps");
  completion.Refresh("/time",5,Suggest);Check(completion.Selected==-1&&!completion.Pending,"editing clears pending selection before Enter execution");
  foreach(float available in new[]{0f,60f,197f,317f,500f,1000f})foreach(float syntax in new[]{20f,54f}){var panel=ChatPanelLayout.Fit(available,syntax,64);Check(panel.Height>=0&&panel.Height<=available&&panel.Rows is >=0 and <=5&&(panel.Rows==0||syntax+44+panel.Rows*ChatPanelLayout.RowHeight<=panel.Height),"suggestion rows stay within available height "+available+"/"+syntax);}
  Check(ChatCommandSyntax.CompletionEnd("/tp @e[type=boss,name=Big Pipe] ~ ~ ~",12)=="/tp @e[type=boss,name=Big Pipe]".Length,"selectors with embedded spaces remain one replacement token");
  string Current(string value,int cursor=-1)=>ChatCommandHints.For(value,cursor<0?value.Length:cursor).Current;
  Check(Current("/give @s ").Contains("item:")&&Current("/give @s item:abc ").Contains("数量"),"parameter hints advance through target, item and quantity");
  Check(Current("/damage \"boss:Big Pipe\" ").Contains("数值"),"quoted names do not shift the parameter position");
  Check(Current("/kill @e[type=boss,name=Big Pipe]").Contains("目标"),"selector spaces do not shift the parameter hint");
  Check(Current("/time ").Contains("query")&&Current("/time query ").Contains("daytime"),"subcommand preview offers branches and narrows after selection");
  Check(Current("/effect clear @s ").Contains("效果")&&!ChatCommandHints.For("/effect clear @s ",17).Syntax.Contains("强度"),"clear effect preview omits give-only arguments");
  Check(Current("/raid time add ").Contains("时长")&&Current("/raid time query ").Contains("参数已填完"),"raid time branches have different argument counts");
  Check(!ChatCommandHints.HasArgument("raid",new[]{"time","query"},2)&&!ChatCommandHints.HasArgument("kill",new[]{"@s"},1)&&ChatCommandHints.HasArgument("effect",new[]{"clear","@s"},2),"completed branches stop suggesting surplus arguments while optional arguments remain available");
  Check(Current("/tp @s ~ ~ ").Contains("<z>")&&Current("/tp ~ ~ ~ facing ~ ").Contains("<y>"),"coordinate and facing previews follow their individual axes");
  Check(Current("/tp marker:home facing ").Contains("朝向"),"teleport facing preview follows a single destination");
  Check(Current("/item modify entity @s Backpack foundInRaid ").Contains("<值>"),"item modify preview tracks nested arguments");
  Check(Current("/give @s item:abc 2",8).Contains("玩家或AI"),"moving back to an earlier argument changes the hint");
  Check(Current("/msg Friend a multi word message ").Contains("消息"),"freeform chat remains on the message parameter");
  Check(ChatCommandHints.For("hello",5).Syntax==""&&ChatCommandHints.For("//time",6).Syntax=="","plain and escaped chat hide command previews");
  Check(ChatCommandHints.For("/tp \"unfinished",15).Syntax.Length>0&&ChatCommandHints.For("/tp ]",5).Syntax.Contains("格式"),"incomplete typing and malformed selectors do not throw from hints");
  foreach(string command in ChatCommandHints.Usage.Keys)Check(ChatCommandHints.For("/"+command+" ",command.Length+2).Syntax.StartsWith("/"+command),"catalog hint available: "+command);
 }
}
