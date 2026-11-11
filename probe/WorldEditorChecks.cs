using System;
using System.Linq;
using System.Text.Json.Nodes;
using Mono.Cecil;
using LocalSpt5Esp;
internal static class WorldEditorChecks {
 internal static void VerifyRuntime(AssemblyDefinition core){
  TypeDefinition Type(string name)=>core.MainModule.Types.Single(t=>t.Name==name);
  MethodReference[] Calls(string type,string method)=>Type(type).Methods.Single(m=>m.Name==method).Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
  void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
  var icons=Type("MenuIcons").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
  Check(!icons.Any(m=>m.DeclaringType.Name=="Matrix4x4"||m.Name=="RotateAroundPivot"||m.Name=="set_matrix"),"icon rendering cannot reenter native Matrix4x4 operators at observed crash site");
  Check(Calls("WorldEditor","ContextRemoveWeapons").Any(m=>m.DeclaringType.Name=="AiEquipmentRemoval"&&m.Name=="RemoveWeapons")&&Calls("WorldEditor","ContextTeam").Any(m=>m.Name=="MarkEditorSide")&&Calls("WorldEditor","ContextPause").Any(m=>m.Name=="ResumeActors"),"context batch actions reuse existing live AI operations");
  Check(Calls("CorpseRevival","Enqueue").Any(m=>m.Name=="ValidateWeapons")&&Calls("CorpseRevival","Prepare").Any(m=>m.Name=="ValidateWeapons"),"unarmed corpses are rejected before queueing and after inventory rebuild");
  var sides=Type("AiActorControl").Methods.Single(m=>m.Name=="CommandEditorSides");
  Check(sides.Body.ExceptionHandlers.Any(h=>h.HandlerType==Mono.Cecil.Cil.ExceptionHandlerType.Finally)&&Calls("AiActorControl","CommandEditorSides").Any(m=>m.Name=="SetRelations")&&!Calls("AiActorControl","CommandEditorSides").Any(m=>m.Name=="Select"),"editor A/B operation restores panel scope in finally and preserves management selection");
  var toggle=Type("WorldEditor").Methods.Single(m=>m.Name=="ToggleShortcut").Body.Instructions;
  Check(toggle.Any(i=>i.Operand is FieldReference f&&f.Name=="dialog")&&toggle.Any(i=>i.Operand is FieldReference f&&f.Name=="cursorOwned"),"toggle permits editor-owned cursor while isolating text dialogs");
  Check(Calls("Plugin","Protected").Any(m=>m.Name=="ProtectMoving")&&Calls("WorldEditor","ProtectMoving").Any(m=>m.Name=="Holding")&&!Calls("WorldEditor","ProtectMoving").Any(m=>m.Name=="ApplyEditorState"),"move protection shares native damage/death protection without overwriting persistent invincibility");
  Check(Type("WorldEditor").Methods.Single(m=>m.Name=="FinishMove").Body.ExceptionHandlers.Any(h=>h.HandlerType==Mono.Cecil.Cil.ExceptionHandlerType.Finally),"move cleanup always releases temporary movement protection");
  Console.WriteLine("PASS crash-site matrix interop removal, corpse weapon preflight and custom AI action routing");
  var selected=Calls("CharacterBuffs","SelectedPlayer");Check(selected.Any(c=>c.DeclaringType.Name=="WorldEditor"&&c.Name=="get_EditingBuffs"),"embedded Buffs participates in AI selection instead of falling back to local player");
  var close=Calls("WorldEditor","CloseDialog");Check(close.Any(c=>c.Name=="ReleasePropertyEditors"),"closing the editor releases embedded selections");
  var release=Calls("WorldEditor","ReleasePropertyEditors");Check(release.Any(c=>c.DeclaringType.Name=="CharacterBuffs"&&c.Name=="CloseAi")&&release.Any(c=>c.DeclaringType.Name=="AiEquipmentEditor"&&c.Name=="Close"),"embedded editor ownership is cleaned up without resetting applied effects");
  var combat=Calls("AiActorControl","ApplyEditorState");Check(combat.Any(c=>c.DeclaringType.Name=="LiveActorReference"&&c.Name=="Resolve")&&!combat.Any(c=>c.Name=="Select"||c.Name=="ApplyBirthState"),"combat property writes validate live identity without changing UI selection or relationship rules");
  Check(Type("WorldEditor").Fields.Single(f=>f.Name=="propertyPages").FieldType.FullName=="System.String[]","AI properties retain independent page routing");
  var drop=Calls("WorldEditor","FinishMove");Check(drop.Any(c=>c.DeclaringType.Name=="AiFreePlacement"&&c.Name=="Place")&&!drop.Any(c=>c.Name=="DirectorMove"||c.Name=="Landing"),"editor drop bypasses navigation landing checks");
  var free=Calls("TeleportControl","FreeMove");Check(free.Any(c=>c.DeclaringType.Name=="AiFreePlacement"&&c.Name=="Place")&&!free.Any(c=>c.Name=="Landing"||c.Name=="Move"),"toy free placement cannot route into ground-only landing");
  var toy=Calls("ToyDeveloper","Tick");Check(toy.Any(c=>c.DeclaringType.Name=="TeleportControl"&&c.Name=="FreeMove"),"toy teleport uses exact-coordinate placement");
  var motion=Calls("MovementAssist","BeforeMove");Check(motion.Any(c=>c.DeclaringType.Name=="AiFreePlacement"&&c.Name=="FilterMotion")&&motion.Any(c=>c.DeclaringType.Name=="WorldEditor"&&c.Name=="FilterMotion"),"air anchors and active batch drags suppress native gravity/motion");
  var anchor=Calls("AiFreePlacement","Place");Check(anchor.Any(c=>c.DeclaringType.Name=="LiveActorReference"&&c.Name=="Resolve")&&anchor.Any(c=>c.DeclaringType.Name=="Player"&&c.Name=="Teleport")&&!anchor.Any(c=>c.Name=="Landing"||c.Name=="HasNavigation"),"free placement validates live identity and directly teleports without rejecting non-navigation points");
  var chatPlacement=Type("TeleportControl").Methods.Single(m=>m.Name=="ChatMove").Body.Instructions.Single(i=>i.Operand is MethodReference method&&method.DeclaringType.Name=="AiFreePlacement"&&method.Name=="Place");Check(chatPlacement.Previous.OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4_0,"chat teleport explicitly disables automatic air anchoring");
  Check(Calls("WorldEditor","CloseDialog").Any(c=>c.DeclaringType.Name=="EditorSelectionRules"&&c.Name=="ClearAfterEditing"),"editor close applies configured single-selection retention");
  var selection=Calls("WorldEditor","SelectBox");Check(selection.Any(c=>c.Name=="Editable")&&selection.Any(c=>c.Name=="InBox"),"marquee uses locally controlled live AI eligibility and screen/depth filtering");
    var through=Type("WorldEditor").Methods.Single(m=>m.Name=="SelectAt").Body.Instructions.Where(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="CrosshairProbe"&&m.Name=="Read").ToArray();Check(through.Length==1&&through[0].Previous.OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4_1,"editor point selection explicitly ignores environment while preserving native body tests");
  Check(!Calls("WorldEditor","InBox").Any(m=>m.Name=="Raycast")&&Calls("WorldEditor","SelectBox").Any(m=>m.DeclaringType.Name=="CorpseReference"&&m.Name=="Read"),"marquee includes corpses behind walls while retaining screen/depth bounds");
  var drawStrings=Type("WorldEditor").Methods.Single(m=>m.Name=="Draw").Body.Instructions.Where(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr).Select(i=>(string)i.Operand).ToArray();Check(!drawStrings.Any(s=>s.Contains("左键放下")||s.Contains("Alt 框选"))&&Calls("WorldEditor","Draw").Any(m=>m.Name=="DrawEditorCrosshair"),"editor restores crosshair independently of hidden instruction HUD");
  var copy=Calls("WorldEditor","PlaceCorpse");Check(copy.Any(m=>m.Name=="CloneItem")&&copy.Any(m=>m.Name=="CreateStillCorpse")&&!copy.Any(m=>m.Name=="Instantiate"),"corpse duplication rebuilds fresh native inventory and corpse instead of cloning live callbacks or deleting the source");
  Check(Calls("WorldEditor","ReviveCorpses").Any(m=>m.DeclaringType.Name=="CorpseRevival"&&m.Name=="Enqueue")&&Calls("WorldEditor","FinishMove").Any(m=>m.Name=="RestoreCorpsePhysics"),"corpse right-click revival uses transactional queue and movement restores physics on exit");
  var add=Calls("AiEquipmentEditor","AddWeapon");Check(add.Any(m=>m.Name=="Target")&&add.Any(m=>m.Name=="get_ContainedItem")&&add.Any(m=>m.DeclaringType.Name=="ItemManipulator"&&m.Name=="Add")&&!add.Any(m=>m.Name.Contains("Remove")),"AI weapon addition validates live target and empty slot, uses native compatibility and never removes an occupied weapon");
  Check(Calls("AiActorControl","MarkEditorSide").Any(m=>m.Name=="ApplyEditorState")&&!Calls("AiActorControl","MarkEditorSide").Any(m=>m.Name=="Select"||m.Name=="SetRelations"),"property A/B marks preserve current management selection and do not implicitly change hostility");
  Console.WriteLine("PASS editor through-wall selection, hidden instruction HUD, corpse snapshot/revival/move routing, empty-slot weapons and A/B property isolation");
  Console.WriteLine("PASS editor/ toy airborne placement routing, physics ownership and live selection safeguards");
  Console.WriteLine("PASS editor embedded Buff AI binding, selected-target validation, relationship isolation and dialog ownership cleanup");
 }
 internal static void Run(){
  void Check(bool value,string reason){if(!value)throw new Exception(reason);}
  var defaults=EditorContextRules.Read(EditorContextRules.Default);
  Check(defaults.Length==21&&defaults.Distinct().Count()==21,"all context actions have stable unique IDs");
  Check(EditorContextRules.Read("copy,unknown,copy, move ,").SequenceEqual(new[]{"copy","move"}),"context settings ignore unknown and duplicate IDs");
  Check(EditorContextRules.Toggle("copy,move","copy")=="move"&&EditorContextRules.Toggle("move","copy")=="move,copy","context toggles preserve order and permit empty menus");
  Check(EditorContextRules.Toggle("copy","copy")==""&&EditorContextRules.Read("").Length==0,"all optional context actions can be hidden");
  Check(EditorContextRules.Move("copy,move","move",-1)=="move,copy"&&EditorContextRules.Move("copy,move","copy",-1)=="copy,move"&&EditorContextRules.Move("copy,move","move",1)=="copy,move","context ordering honors list boundaries");
  Check(EditorContextRules.Read("resume,copy,pause,invincible-off,invincible-on,ab-peace,ab-hostile,player-hostile,player-peace").SequenceEqual(new[]{"pause-toggle","copy","invincible-toggle","ab-toggle","player-toggle"}),"legacy pairs merge once at their first visible position");
  Check(EditorContextRules.EnableNext(new[]{false,false})&&EditorContextRules.EnableNext(new[]{true,false})&&!EditorContextRules.EnableNext(new[]{true,true})&&!EditorContextRules.EnableNext(Array.Empty<bool>()),"toggles unify mixed selections and reverse uniform selections");
  Console.WriteLine("PASS configurable context action IDs, filtering, visibility and ordering boundaries");
  foreach(HotkeyModifiers required in Enumerable.Range(0,8).Select(i=>(HotkeyModifiers)i))foreach(HotkeyModifiers held in Enumerable.Range(0,8).Select(i=>(HotkeyModifiers)i)){
   Check(HotkeyRules.Matches(required,held)==(required==held),"hotkey chords require exact modifiers outside editor selection");
   Check(HotkeyRules.Matches(required,held,true)==(required==held||(held&~HotkeyModifiers.Alt)==required),"editor selection permits extra Alt while retaining exact Ctrl/Shift requirements");
  }
  Check(!HotkeyRules.Matches(HotkeyModifiers.Control,HotkeyModifiers.Control|HotkeyModifiers.Shift,true)&&HotkeyRules.Matches(HotkeyModifiers.Control,HotkeyModifiers.Control|HotkeyModifiers.Alt,true),"Alt selection keeps Ctrl+C usable without accepting Ctrl+Shift+C");
  Console.WriteLine("PASS configurable hotkey chord modifier boundaries and editor Alt-selection compatibility");
  Check(HotkeyRules.LegacyControl(3,"People toggle")&&HotkeyRules.LegacyControl(8,"Cycle mode key")&&HotkeyRules.LegacyControl(29,"Heal key")&&!HotkeyRules.LegacyControl(8,"Hold key")&&!HotkeyRules.LegacyControl(14,"Panel hold key")&&!HotkeyRules.LegacyControl(64,"Chat key"),"legacy Ctrl toggles display their actual chord without changing held keys or chat bindings");
  foreach(string mode in new[]{"","context"}){
   Check(EditorShortcutRules.Allowed(mode,false,false,false,true),"shortcuts work in scene selection and context menus");
   Check(!EditorShortcutRules.Allowed(mode,true,false,false,true)&&!EditorShortcutRules.Allowed(mode,false,true,false,true),"active movement and marquee dragging suppress copy/paste/delete");
   Check(!EditorShortcutRules.Allowed(mode,false,false,true,true)&&!EditorShortcutRules.Allowed(mode,false,false,false,false),"external text input, focus loss and unrelated cursors suppress editor commands");
  }
  foreach(string mode in new[]{"properties","rename","create"})Check(!EditorShortcutRules.Allowed(mode,false,false,false,true),"typing in editor fields cannot copy, paste or delete scene objects");
  Console.WriteLine("PASS editor shortcut modifiers, text-entry isolation, external-input ownership and active-drag/move suppression");
  Check(EditorSelectionRules.ClearAfterEditing("properties",1,false)&&EditorSelectionRules.ClearAfterEditing("rename",1,false),"single-target property/rename close clears selection by default");
  Check(!EditorSelectionRules.ClearAfterEditing("properties",1,true)&&!EditorSelectionRules.ClearAfterEditing("properties",2,false)&&!EditorSelectionRules.ClearAfterEditing("context",1,false)&&!EditorSelectionRules.ClearAfterEditing("create",1,false),"retention setting, multiple targets and non-edit dialogs preserve selection");
  foreach(float width in new[]{240f,432f,440f,600f,824f,872f,890f,1280f})foreach(float height in new[]{160f,280f,412f,560f,760f,1080f})foreach(bool embedded in new[]{false,true}){
   var frame=AiEditorLayout.Frame(width,height,"equipment",embedded);float header=AiEditorLayout.EquipmentHeader(frame.Width,embedded);
   // Helmet plus removable attachment strip is the tallest branch before fields.
   float fieldsTop=header+44+48+78+48,footerTop=frame.Height-124;
   Check(fieldsTop+120<=footerTop,"equipment parameters retain their viewport without intersecting apply/reset/notice controls");
   Check(frame.Width>=432&&frame.Height>=height&&(frame.Scroll||frame.Width<=width&&frame.Height<=height),"compact frames scroll rather than drawing outside the parent window");
   int columns=AiEditorLayout.EquipmentColumns(frame.Width);Check((frame.Width-8*(columns-1))/columns>=100,"equipment tabs keep enough width for readable labels");
  }
  foreach(string page in new[]{"weapon","storage"})foreach(float height in new[]{160f,300f,420f,900f}){var frame=AiEditorLayout.Frame(824,height,page);Check(frame.Height>=(page=="weapon"?620:600)&&frame.Scroll==(height<frame.Height),"weapon and storage subpages preserve room for their nested fields and actions");}
  Check(AiEditorLayout.Frame(824,412,"equipment",true).Scroll&&!AiEditorLayout.Frame(1280,900,"equipment").Scroll,"embedded raid editor scrolls while full standalone panels keep their normal layout");
  Console.WriteLine("PASS AI equipment slot wrapping, short embedded viewport/footer separation and bounded weapon/storage subpages");
  void Reject(string text,string root){try{int id=100;EditorItemRules.Remap(text,root,()=> (++id).ToString("x24"),out _);}catch(ArgumentException){return;}throw new Exception("Invalid item tree was accepted.");}
  const string root="000000000000000000000001",tpl="5449016a4bdc2d6f028b456f";
  var source=new JsonArray();
  for(int i=1;i<=5;i++){var entry=new JsonObject{["_id"]=i.ToString("x24"),["_tpl"]=tpl,["parentId"]=(i==1?99:i-1).ToString("x24"),["slotId"]=i==1?"hideout":"main",["location"]=new JsonObject{["x"]=i,["y"]=2,["r"]=0},["upd"]=new JsonObject{["StackObjectsCount"]=7,["SpawnedInSession"]=true,["Repairable"]=new JsonObject{["Durability"]=38,["MaxDurability"]=50}}};source.Add(entry);}
  string original=source.ToJsonString();int seed=100;var copied=EditorItemRules.Remap(original,root,()=> (++seed).ToString("x24"),out string fresh);
  Check(original==source.ToJsonString()&&copied.Count==5&&fresh!=root,"copy leaves source untouched and allocates a new complete tree");
  Check(copied[0]!["parentId"]==null&&copied[0]!["slotId"]==null&&copied[0]!["location"]==null,"world root is detached from its previous inventory address");
  for(int i=1;i<copied.Count;i++){Check(copied[i]!["parentId"]!.GetValue<string>()==copied[i-1]!["_id"]!.GetValue<string>(),"nested equipment, grids and attachments keep their new parent");Check(copied[i]!["location"]!.ToJsonString()==source[i]!["location"]!.ToJsonString(),"nested grid placements remain intact");}
  for(int i=0;i<copied.Count;i++)Check(copied[i]!["upd"]!.ToJsonString()==source[i]!["upd"]!.ToJsonString(),"stack, durability and found-in-raid state survive copying");
  var duplicate=JsonNode.Parse(original)!.AsArray();duplicate[1]!["_id"]=root;Reject(duplicate.ToJsonString(),root);
  var cycle=JsonNode.Parse(original)!.AsArray();cycle[1]!["parentId"]=5.ToString("x24");Reject(cycle.ToJsonString(),root);
  var orphan=JsonNode.Parse(original)!.AsArray();orphan[1]!["parentId"]=99.ToString("x24");Reject(orphan.ToJsonString(),root);
  Reject("[]",root);Reject(original,99.ToString("x24"));
  try{EditorItemRules.Remap(original,root,()=>root,out _);throw new Exception("ID collision accepted");}catch(ArgumentException){}
  Check(EditorItemRules.Distance(2,-20)==1&&EditorItemRules.Distance(80,20)==100&&EditorItemRules.Distance(10,1)>10,"holding distance clamps both ends and responds to wheel");
  try{EditorItemRules.Distance(float.NaN,1);throw new Exception("Invalid distance accepted");}catch(ArgumentException){}
  var gate=new MenuInputGate();gate.SetOpen(true,1);gate.SetOpen(false,2);gate.Tick(3,true);Check(gate.Blocked,"initiating menu click cannot drop a moved entity or fire");gate.Tick(4,false);Check(!gate.Blocked,"release restores camera movement input");
  Check(MenuSections.Pages("开发工具").Contains("编辑模式")&&MenuSections.Group("63. 场景编辑")=="开发工具"&&MenuSections.OnPage("开发工具","编辑模式","63. 场景编辑","Editor mode"),"editor config is discoverable from its development page");
  bool[] actions={true,false,false,true,false,true};
  Check(EditorMenuRules.Next(actions,0,1)==3&&EditorMenuRules.Next(actions,3,1)==5&&EditorMenuRules.Next(actions,5,1)==0,"wheel moves down through enabled actions and wraps");
  Check(EditorMenuRules.Next(actions,0,-1)==5&&EditorMenuRules.Next(actions,5,-1)==3,"wheel up reverses selection and skips disabled actions");
  Check(EditorMenuRules.Next(actions,2,0)==3&&EditorMenuRules.Next(actions,-1,1)==0&&EditorMenuRules.Next(actions,99,-1)==5,"invalid/disabled selection recovers without activating a disabled action");
  Check(EditorMenuRules.Next(Array.Empty<bool>(),0,1)==-1&&EditorMenuRules.Next(new[]{false,false},0,1)==-1&&EditorMenuRules.Next(new[]{false,true,false},1,1)==1,"empty/disabled menus and single enabled action terminate correctly");
  foreach(var size in new[]{(1920f,1080f),(800f,600f),(240f,180f),(12f,10f)})foreach(float anchor in new[]{-100f,0f,5000f}){var popup=EditorMenuRules.Popup(anchor,anchor,252,282,size.Item1,size.Item2);Check(popup.X>=0&&popup.Y>=0&&popup.X+popup.Width<=size.Item1&&popup.Y+popup.Height<=size.Item2,"context popup remains inside narrow or edge-aligned screens");}
  Console.WriteLine("PASS editor wheel navigation, disabled-action skipping, wrapping, invalid selection recovery and popup screen bounds");
  var box=EditorSelectionRules.Box(500,300,100,80);Check(box.X==100&&box.Y==80&&box.Width==400&&box.Height==220,"reverse-direction drag normalizes its rectangle");
  Check(EditorSelectionRules.Contains(box,100,80,1,1920,1080)&&EditorSelectionRules.Contains(box,500,300,100,1920,1080),"projected object centers on either selection edge are included");
  Check(!EditorSelectionRules.Contains(box,200,200,-1,1920,1080)&&!EditorSelectionRules.Contains(box,200,200,0,1920,1080)&&!EditorSelectionRules.Contains(box,float.NaN,200,1,1920,1080)&&!EditorSelectionRules.Contains(box,200,float.PositiveInfinity,1,1920,1080)&&!EditorSelectionRules.Contains(box,200,200,float.NaN,1920,1080),"camera-backside, near-plane and non-finite projections cannot be selected");
  Check(!EditorSelectionRules.Contains(box,501,200,1,1920,1080)&&!EditorSelectionRules.Contains(EditorSelectionRules.Box(-500,-500,2500,2500),-1,200,1,1920,1080)&&!EditorSelectionRules.Contains(EditorSelectionRules.Box(-500,-500,2500,2500),2000,200,1,1920,1080),"off-screen and out-of-box projections are excluded even by a large drag");
  var originals=new[]{new System.Numerics.Vector3(1,0,5),new System.Numerics.Vector3(5,4,9)};var goal=new System.Numerics.Vector3(20,100,-30);var placed=EditorSelectionRules.Translate(originals,goal);
  Check((placed[0]+placed[1])/2==goal&&placed[1]-placed[0]==originals[1]-originals[0]&&originals[0].Y==0,"batch placement preserves relative XYZ spacing, centers on an airborne goal and leaves source untouched");
  Check(EditorSelectionRules.Translate(new[]{originals[0]},goal).Single()==goal,"single-object placement uses exact target without a landing offset");
  foreach(var invalid in new[]{Array.Empty<System.Numerics.Vector3>(),new[]{new System.Numerics.Vector3(float.NaN,0,0)}})try{EditorSelectionRules.Translate(invalid,goal);throw new Exception("Invalid selection accepted");}catch(ArgumentException){}
  try{EditorSelectionRules.Translate(originals,new(float.PositiveInfinity,0,0));throw new Exception("Invalid goal accepted");}catch(ArgumentException){}
  Console.WriteLine("PASS marquee reverse dragging, screen/depth/finite boundaries, airborne batch centering and relative-spacing preservation");
  Console.WriteLine("PASS editor nested item cloning, fresh identities, detached root, unchanged source, state/grid preservation, corrupt-tree rejection, distance and input-release boundaries");
 }
}
