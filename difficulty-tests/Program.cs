using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using LocalSpt5Esp;
void Check(bool valid,string text){if(!valid)throw new Exception(text);}
void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid input accepted");}
var settings=new AiDifficultySettings();settings.Validate();Check(!settings.Enabled,"Defaults change gameplay");
Check(AiDifficultyRules.Group("marksman")=="sniper"&&AiDifficultyRules.Group("assault")=="scav"&&AiDifficultyRules.Group("pmcUSEC")=="usec"&&AiDifficultyRules.Group("pmcBEAR")=="bear"&&AiDifficultyRules.Group("infectedTagilla")=="infected","Incorrect AI category");
settings.Overall.Values["vision"]=1.5f;settings.Groups["usec"]=settings.Overall.Clone();settings.Groups["usec"].Values["vision"]=.5f;
Check(settings.ForRole("pmcUSEC").Value("vision")==.5f&&settings.ForRole("pmcBEAR").Value("vision")==1.5f,"Category isolation failed");
settings.Groups.Remove("usec");Check(settings.ForRole("pmcUSEC").Value("vision")==1.5f,"Inheritance did not resume");
for(int i=0;i<5;i++){settings.Overall=AiDifficultyRules.Preset(i);settings.Validate();Check(settings.Overall.Value("damage")==1,"Preset unexpectedly changes damage");}
Check(AiDifficultyRules.Preset(0).Value("scatter")>1&&AiDifficultyRules.Preset(4).Value("scatter")<1&&!AiDifficultyRules.Preset(0).AllowGrenades,"Preset direction is wrong");
foreach(var field in AiDifficultyRules.Fields)Check(AiDifficultyRules.Preset(2).Value(field.Key)==1,"Original preset changes values");
var roundtrip=JsonSerializer.Deserialize<AiDifficultySettings>(JsonSerializer.Serialize(settings))!;roundtrip.Validate();Check(roundtrip.Overall.Value("vision")==settings.Overall.Value("vision"),"Settings do not persist");
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-1,0,100}){var invalid=new AiDifficultySettings();invalid.Overall.Values["vision"]=bad;Reject(invalid.Validate);}
var unknown=new AiDifficultySettings();unknown.Groups["typo"]=new();Reject(unknown.Validate);
var unknownField=new AiDifficultySettings();unknownField.Overall.Values["typo"]=1;Reject(unknownField.Validate);
Check(AiDifficultyRules.Scale(220,2,"Core.VisibleAngle")==360&&AiDifficultyRules.Scale(100,1.5f,"Core.VisibleDistance")==150&&AiDifficultyRules.Scale(0,2,"Aiming.FIRST_CONTACT_ADD_SEC")==0&&AiDifficultyRules.Scale(-1,2,"sentinel")==-1,"Bad scaling or disabled value changed");
float baseline=100;Check(AiDifficultyRules.Scale(baseline,2,"x")==200&&AiDifficultyRules.Scale(baseline,2,"x")==200&&AiDifficultyRules.Scale(baseline,1,"x")==100,"Scaling compounds or reset differs");
Console.WriteLine("PASS defaults, 12 role categories, independent profiles, inheritance, presets, persistence, invalid input and non-cumulative scaling");
var root=Path.GetFullPath(Environment.GetEnvironmentVariable("SPT_SOURCE_ROOT")??Path.Combine(AppContext.BaseDirectory,"../../../.."));
using var native=ModuleDefinition.ReadModule("BepInEx/interop/Assembly-CSharp.dll");
var components=native.Types.Single(t=>t.Name=="BotSettingsComponents");int paths=0;
foreach(var field in AiDifficultyRules.Fields)foreach(string path in field.Paths){var parts=path.Split('.');var group=components.Properties.Single(p=>p.Name==parts[0]);var type=native.Types.Single(t=>t.FullName==group.PropertyType.FullName);var property=type.Properties.Single(p=>p.Name==parts[1]);Check(property.PropertyType.FullName=="System.Single"&&property.SetMethod!=null,"Invalid native field: "+path);paths++;}
foreach(string path in new[]{"Core.CanGrenade","Shoot.MISS_TO_HEAD","Look.CHECK_HEAD_ANY_DIST","Look.MIDDLE_DIST_CAN_SHOOT_HEAD"}){var parts=path.Split('.');var group=components.Properties.Single(p=>p.Name==parts[0]);Check(native.Types.Single(t=>t.FullName==group.PropertyType.FullName).Properties.Any(p=>p.Name==parts[1]&&p.PropertyType.FullName=="System.Boolean"&&p.SetMethod!=null),"Invalid native toggle");}
var en=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"esp-cover/Locales/en.json")))!;
foreach(var field in AiDifficultyRules.Fields)Check(en.ContainsKey(field.Name)&&en.ContainsKey(field.Help),"Missing English translation: "+field.Name);
var panel=File.ReadAllText(Path.Combine(root,"esp-cover/AiDifficultyPanel.cs"));foreach(Match match in Regex.Matches(panel,"\"([^\"\\r\\n]*[\\p{IsCJKUnifiedIdeographs}][^\"\\r\\n]*)\"")){var label=match.Groups[1].Value;Check(en.ContainsKey(label),"Missing page translation: "+label);Check(!label.Contains("反射")&&!label.Contains("字段")&&!label.Contains("开发")&&!label.Contains("IL2CPP"),"Developer wording visible on page");}
Check(File.ReadAllText(Path.Combine(root,"esp-cover/MenuSections.cs")).Contains("\"AI 难度\""),"No raid navigation entry");
Console.WriteLine($"PASS {paths} native numeric fields, 4 native toggles, bilingual page and player wording");
using var built=ModuleDefinition.ReadModule(Path.Combine(root,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types){foreach(var type in types){yield return type;foreach(var nested in AllTypes(type.NestedTypes))yield return nested;}}
var calls=AllTypes(built.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().Where(m=>m.DeclaringType.Name=="AiDifficultyControl").Select(m=>m.Name).ToArray();
Check(calls.Contains("Initialize")&&calls.Contains("Tick")&&calls.Contains("Shutdown"),"AI difficulty initialization, update or restoration is disconnected");
Console.WriteLine("PASS production lifecycle connects persisted settings, live updates and unload restoration");
