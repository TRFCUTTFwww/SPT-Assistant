using ProjectE.Client;
using Mono.Cecil;
int count=0;
void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
Check(ProjectAudioRules.ValidClip(true,44100,1,44100,true),"live mono PCM data can be reused");
Check(ProjectAudioRules.ValidClip(true,96000,2,48000,true),"live stereo PCM data can be reused");
Check(!ProjectAudioRules.ValidClip(false,44100,1,44100,true),"destroyed native clips are reloaded");
Check(!ProjectAudioRules.ValidClip(true,0,1,44100,true),"the observed live cache entry with zero samples is invalid");
Check(!ProjectAudioRules.ValidClip(true,44100,0,44100,true)&&!ProjectAudioRules.ValidClip(true,44100,1,0,true),"missing native channels or rate require rebuilding");
Check(!ProjectAudioRules.ValidClip(true,44100,1,44100,false),"unloaded/failed/loading audio data cannot silently reuse a cache entry");
using var assembly=AssemblyDefinition.ReadAssembly(args[0]);var resource=assembly.MainModule.Types.Single(t=>t.Name=="ProjectResources");
MethodReference[] Calls(string name)=>resource.Methods.Single(m=>m.Name==name).Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(Calls("Play").Any(m=>m.Name=="ValidClip")&&Calls("Play").Any(m=>m.Name=="get_samples")&&Calls("Play").Any(m=>m.Name=="get_loadState"),"playback validates actual native PCM state instead of dictionary membership alone");
Check(Calls("Play").Any(m=>m.Name=="set_hideFlags")&&Calls("Play").Any(m=>m.Name=="SetData")&&Calls("Play").Any(m=>m.Name=="set_Item"),"recreated native audio is retained, filled with PCM and replaces the old cache entry");
Check(Calls("Play").Any(m=>m.Name=="EnsureAudioSource")&&Calls("Play").Any(m=>m.Name=="PlayOneShot"),"every playback request restores the owned source before submitting its voice");
Check(new[]{"set_enabled","SetActive","set_mute","set_volume","set_pitch","set_priority","set_ignoreListenerPause","set_spatialBlend"}.All(name=>Calls("EnsureAudioSource").Any(m=>m.Name==name)),"source recovery covers component/object lifecycle, playback gain, pause and voice priority");
Check(!resource.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:[]).Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.DeclaringType.Name=="AudioListener"||m.DeclaringType.Name=="AudioSettings"),"audio repair never changes global volume, listener pause or engine audio settings");
Check(Calls("Clear").Any(m=>m.Name=="Destroy")&&Calls("Clear").Any(m=>m.Name=="Clear"),"module cleanup still destroys owned native clips and audio source");
Console.WriteLine($"{count} ProjectE audio lifecycle checks passed.");
