using LocalSpt5Esp;
using System.Text.Json;
internal static class ItemSearchChecks {
 internal static void Run(){
  static void Check(bool value,string reason){if(!value)throw new Exception(reason);Console.WriteLine("PASS "+reason);}
  var catalog=new ChatItemSearch(new[]{
   new ChatItemSearch.Item("med1","Salewa急救包","Salewa first aid kit",false,new[]{"Salewa"}),
   new ChatItemSearch.Item("med2","IFAK个人战术急救包","IFAK individual first aid kit",false,new[]{"IFAK"}),
   new ChatItemSearch.Item("ammo1","5.56x45毫米 M855A1","5.56x45mm M855A1",true,new[]{"M855A1"}),
   new ChatItemSearch.Item("ammo2","5.56x45毫米 M855","5.56x45mm M855",true,new[]{"M855"}),
   new ChatItemSearch.Item("dup1","重复名","Duplicate name",false,Array.Empty<string>()),
   new ChatItemSearch.Item("dup2","重复名","Duplicate name",false,Array.Empty<string>())});
  Check(catalog.Resolve("sAlEwA")=="med1"&&catalog.Resolve("IFAK")=="med2","English aliases resolve without item prefix or template IDs");
  Check(catalog.Resolve("战术急救")=="med2"&&catalog.Resolve("aid salewa")=="med1","Chinese substring and reordered English words resolve uniquely");
  Check(catalog.Resolve("M855")=="ammo2"&&catalog.Search("M855",true)[0].Id=="ammo2","exact ammunition alias ranks before longer overlapping names");
  Check(catalog.Search("first aid").Length==2&&catalog.Search("first aid",true).Length==0,"search matches inside a name and ammo candidates exclude medical items");
  Check(catalog.Resolve("item:med1")=="med1"&&catalog.Resolve("5.56×45mm M855A1")=="ammo1","legacy IDs and caliber punctuation remain accepted");
  bool ambiguous=false;try{catalog.Resolve("first aid");}catch(ArgumentException e){ambiguous=e.Message.Contains("↑↓");}Check(ambiguous,"ambiguous search explains how to choose instead of resolving an arbitrary item");
  Check(catalog.Insert(catalog.Search("Duplicate name")[0]).StartsWith("item:"),"duplicate names insert an unambiguous template ID with a readable display label");
  foreach(var item in catalog.Search("")){string insertion=catalog.Insert(item);Check(catalog.Resolve(ChatCommandSyntax.Tokens(ChatCommandSyntax.Quote(insertion))[0])==item.Id,"suggested item roundtrips into exact execution: "+item.Id);}
  Check(catalog.Search("!!!").Length==0,"punctuation-only search does not select every item");
  string? game=Environment.GetEnvironmentVariable("SPT_GAME_ROOT");if(string.IsNullOrEmpty(game))return;
  string data=Path.Combine(game,"SPT_Runtime","SPT_Data","database");
  Dictionary<string,string> Locale(string language)=>JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(data,"locales","global",language+".json")))!;
  using var templates=JsonDocument.Parse(File.ReadAllText(Path.Combine(data,"templates","items.json")));
  var real=ChatItemSearch.Read(templates.RootElement,new Dictionary<string,string>(),Locale("en"),Locale("ch"));
  foreach(string query in new[]{"salewa","IFAK","bitcoin","比特币","M855A1"}){
   var matches=real.Search(query);Check(matches.Length>0,"installed item catalog finds "+query);
   Check(matches.Take(64).All(item=>real.Resolve(real.Insert(item))==item.Id),"installed suggestions resolve without name ambiguity: "+query);
  }
  Check(real.Search("5.56 M855",true).Length>0,"installed ammo accepts separate caliber and bullet keywords");
 }
}
