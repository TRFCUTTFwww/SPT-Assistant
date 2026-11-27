using System.Text.Json;
using EFT;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class NativeFleaOfferEdit {
 static BepInEx.Configuration.ConfigEntry<bool> enabled=null!,withdrawEnabled=null!;static EventHandler? changed;
 internal static bool Enabled=>enabled?.Value==true;
 internal static bool WithdrawEnabled=>withdrawEnabled?.Value==true;
 internal static bool Busy=>withdrawing!=null;
 internal static void Toggle()=>enabled.Value=!enabled.Value;
 internal static void ToggleWithdraw()=>withdrawEnabled.Value=!withdrawEnabled.Value;
 sealed class Buttons(OfferView view){internal readonly OfferView View=view;internal DefaultUIButton? Edit,Withdraw;}
 static readonly Dictionary<IntPtr,Buttons> buttons=new();
 static readonly Dictionary<IntPtr,OfferView> views=new();
 static readonly List<(InteractionButtonsContainer Container,SimpleContextMenuButton Button)> menuButtons=[];
 static Task<string>? withdrawing,timingRead;static string withdrawalTemplate="",timingError="";
 static float nextTimingRead;static long timingRevision;
 internal static void Initialize(Plugin plugin){
  BepInEx.Configuration.ConfigEntry<bool> Bind(string key,string label,string help)=>plugin.Config.Bind("Flea Market",key,false,new BepInEx.Configuration.ConfigDescription(help,null,new CnConfig.ConfigurationManagerAttributes{DispName=label,Category=MenuSections.FleaMarket}));
  enabled=Bind("Show edit offer button","显示修改按钮","开启后在原生报价操作区与右键菜单显示修改。");
  withdrawEnabled=Bind("Show withdraw offer button","显示下架按钮","开启后在原生报价操作区与右键菜单显示下架；下架后保存到 SQLite，可重新上架。");
  changed=(_,_)=>{ClearMenus();RefreshAll();};enabled.SettingChanged+=changed;withdrawEnabled.SettingChanged+=changed;
  plugin.Patches.Patch(AccessTools.Method(typeof(OfferView),"Tick"),postfix:new HarmonyMethod(typeof(NativeFleaOfferEdit),nameof(Timing)));
  plugin.Patches.Patch(AccessTools.Method(typeof(OfferView),"Update"),postfix:new HarmonyMethod(typeof(NativeFleaOfferEdit),nameof(UpdateTiming)));
  foreach(string method in new[]{nameof(OfferView.ShowAction),nameof(OfferView.SetExpandedStatus)})
   plugin.Patches.Patch(AccessTools.Method(typeof(OfferView),method),postfix:new HarmonyMethod(typeof(NativeFleaOfferEdit),nameof(Selected)));
  foreach(string method in new[]{"_Awake_b__101_0","_Awake_b__101_1"})
   plugin.Patches.Patch(AccessTools.Method(typeof(OfferView),method),postfix:new HarmonyMethod(typeof(NativeFleaOfferEdit),nameof(Context)));
 }
 static void Show(OfferView view){try{
  if(!Enabled||Busy||view?.Offer==null)return;
  view._itemUiContext?.ContextMenu?.Close();FleaOfferEditor.Show(view.Offer);
 }catch(Exception e){FeatureToasts.Show(e.Message);}}
 static bool Editable(OfferView view)=>view.Offer!=null&&!view.IsTraderOffer&&!view.IsMyOffer;
 static void Withdraw(OfferView view){try{
  if(!WithdrawEnabled||Busy||!Editable(view))return;
  view._itemUiContext?.ContextMenu?.Close();withdrawalTemplate=view.Offer.Item?.StringTemplateId??"";
  withdrawing=CharacterDataPanel.Call("/local/gear/data","characterData",new Dictionary<string,object?>{["action"]="fleaOfferWithdraw",["id"]=view.Offer.Id,["template"]=withdrawalTemplate}).WaitAsync(TimeSpan.FromSeconds(20));
  RefreshAll();FeatureToasts.Show("正在下架报价…");
 }catch(Exception e){FeatureToasts.Show(e.Message);}}
 internal static void Tick(){
  if(timingRead?.IsCompleted==true){try{
   using var doc=JsonDocument.Parse(timingRead.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0)throw new ArgumentException("剩余时间读取失败");
   if(timingRevision==FleaMarketPanel.OfferRevision)FleaMarketPanel.OffersChanged(CharacterDataPanel.Data(root),"");else nextTimingRead=0;
   timingError="";
  }catch{timingError="剩余时间读取失败";}finally{timingRead=null;RefreshTimingAll();}}
  if(withdrawing?.IsCompleted!=true)return;
  try{
   using var doc=JsonDocument.Parse(withdrawing.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var message)?message.GetString():"下架失败。");
   FleaMarketPanel.OffersChanged(CharacterDataPanel.Data(root),withdrawalTemplate);FeatureToasts.Show("报价已下架并保存，刷新游戏跳蚤列表即可查看。");
  }catch(Exception e){FeatureToasts.Show(e is TimeoutException?"连接超时":e.Message);}finally{withdrawing=null;RefreshAll();}
 }
 static DefaultUIButton Create(OfferView view,string name,Action click){
  var button=UnityEngine.Object.Instantiate(view._purchaseButton,view._buttonsContainer.transform,false);button.gameObject.name=name;
  // Replace the entire cloned event, including persistent native purchase listeners.
  button.OnClick=new UnityEngine.Events.UnityEvent();button.OnClick.AddListener((UnityEngine.Events.UnityAction)(()=>click()));return button;
 }
 static void Configure(OfferView view,DefaultUIButton? button,bool visible,string caption,int slot){
  if(button==null)return;button.SetRawText(I18n.Text(caption),Math.Max(12,view._purchaseButton.HeaderSize));button.Interactable=!Busy;
  if(view._buttonsContainer.GetComponent<UnityEngine.UI.LayoutGroup>()==null){
   var rect=button.transform.TryCast<RectTransform>();var source=view._purchaseButton.transform.TryCast<RectTransform>();
   if(rect!=null&&source!=null){rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,80);rect.anchoredPosition=source.anchoredPosition+new Vector2(-source.rect.width*source.pivot.x-8-80*(1-rect.pivot.x)-slot*88,0);}
  }
  button.gameObject.SetActive(visible&&Editable(view));
 }
 static void Refresh(Buttons pair){
  var view=pair.View;if(view==null||view._buttonsContainer==null||view._purchaseButton==null)return;
  if(Enabled&&pair.Edit==null)pair.Edit=Create(view,"AssistantFleaEdit",()=>Show(view));
  if(WithdrawEnabled&&pair.Withdraw==null)pair.Withdraw=Create(view,"AssistantFleaWithdraw",()=>Withdraw(view));
  Configure(view,pair.Edit,Enabled,"修改",0);Configure(view,pair.Withdraw,WithdrawEnabled,"下架",Enabled?1:0);
 }
 static void RefreshAll(){foreach(var pair in buttons.Values)try{Refresh(pair);}catch{}}
 static void Selected(OfferView __instance){try{
  if(__instance!=null&&__instance.Pointer!=IntPtr.Zero)views[__instance.Pointer]=__instance;
  if(__instance!=null)Timing(__instance);
  if(!buttons.TryGetValue(__instance!.Pointer,out var pair)){
   if(!Enabled&&!WithdrawEnabled)return;buttons[__instance.Pointer]=pair=new(__instance);
  }
  Refresh(pair);
 }catch(Exception e){Plugin.Self?.Log.LogWarning("Flea offer actions: "+e.Message);}}
 static void SetLabel(OfferView view,string text){
  if(view._expirationLabel==null)return;
  string formatted="\u00A0"+text+"\u00A0";
  if(view._expirationLabel.text!=formatted)view._expirationLabel.SetMonospaceText(formatted,true);
 }
 static void EnsureTimingHydrated(){
  if(timingRead!=null||Time.unscaledTime<nextTimingRead)return;
  timingRevision=FleaMarketPanel.OfferRevision;nextTimingRead=Time.unscaledTime+12;
  timingRead=CharacterDataPanel.Call("/local/gear/data","characterData",new Dictionary<string,object?>{["action"]="fleaPriceStatus"}).WaitAsync(TimeSpan.FromSeconds(20));
 }
 static void UpdateTiming(OfferView __instance){try{
  if(__instance==null||__instance.Offer==null||__instance._expirationLabel==null)return;
  var offer=FleaMarketPanel.FindOffer(__instance.Offer.Id);
  if(offer!=null&&(offer.Lifetime.Paused||offer.Lifetime.IsPermanent))Timing(__instance);
 }catch{}}
 static void Timing(OfferView __instance){try{
  if(__instance==null||__instance.Offer==null||__instance._expirationLabel==null)return;
  if(__instance.Pointer!=IntPtr.Zero)views[__instance.Pointer]=__instance;
  var offer=FleaMarketPanel.FindOffer(__instance.Offer.Id);
  if(offer!=null){
   var lifetime=offer.Lifetime;long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   SetLabel(__instance,lifetime.Paused?I18n.Text("已暂停")+" "+lifetime.Clock(now):lifetime.IsPermanent?I18n.Text("永久"):lifetime.Clock(now));
   return;
  }
  if(!FleaMarketPanel.HasOffersLoaded&&Editable(__instance))EnsureTimingHydrated();
  if(FleaOfferEditor.NativeExpiry(__instance.Offer)>=LocalFleaShared.FleaOfferLifetime.PermanentEndTime){
   EnsureTimingHydrated();
   SetLabel(__instance,I18n.Text(timingError.Length>0?timingError:timingRead!=null?"正在读取剩余时间…":"永久"));
  }
 }catch{}}
 internal static void RefreshTimingAll(){
  if(views.Count==0)return;
  IntPtr[]? dead=null;
  foreach(var (ptr,view) in views){
   try{
    if(view==null||view.Pointer==IntPtr.Zero||view.gameObject==null){(dead??=new IntPtr[views.Count])[0]=ptr;continue;}
    if(view.gameObject.activeInHierarchy)Timing(view);
   }catch{}
  }
  if(dead!=null)foreach(var ptr in views.Keys.Where(k=>{try{return views[k]==null||views[k].Pointer==IntPtr.Zero;}catch{return true;}}).ToArray())views.Remove(ptr);
 }
 static void Context(OfferView __instance){try{
  var menu=__instance._itemUiContext?.ContextMenu;
  if(menu==null||!menu.gameObject.activeInHierarchy||!Editable(__instance)||(!Enabled&&!WithdrawEnabled))return;
  ClearMenus();var container=menu._interactionButtonsContainer;if(container?._buttonsContainer==null)return;
  var view=__instance;
  void Add(string id,string caption,Action click){
   var button=container.CreateContextButton(id,I18n.Text(caption),container._buttonTemplate,container._buttonsContainer,null,click,(Action)(()=>{}),false,true);
   if(!Busy)button.SetButtonInteraction(Comfort.Common.SuccessfulResult.New);menuButtons.Add((container,button));
  }
  if(Enabled)Add("assistant.flea-edit","修改",()=>Show(view));
  if(WithdrawEnabled)Add("assistant.flea-withdraw","下架",()=>Withdraw(view));
 }catch(Exception e){Plugin.Self?.Log.LogWarning("Flea offer actions menu: "+e.Message);}}
 static void ClearMenus(){
  foreach(var (container,button) in menuButtons)try{
   if(container==null||button==null||!ItemFirContext.IsAssistantButton(button,typeof(NativeFleaOfferEdit).FullName))continue;
   if(container._buttonInstancePools?.TryGetValue(button,out var pool)==true){container._buttonInstancePools.Remove(button);if(pool._poolInUse?.Contains(button)==true){pool.Release(button);continue;}}
   button.Close();button.gameObject.SetActive(false);
  }catch{}
  menuButtons.Clear();
 }
 internal static void Reset(){
  if(changed!=null){enabled.SettingChanged-=changed;withdrawEnabled.SettingChanged-=changed;}changed=null;ClearMenus();
  foreach(var pair in buttons.Values){if(pair.Edit!=null)UnityEngine.Object.Destroy(pair.Edit.gameObject);if(pair.Withdraw!=null)UnityEngine.Object.Destroy(pair.Withdraw.gameObject);}buttons.Clear();views.Clear();withdrawing=timingRead=null;nextTimingRead=0;timingError="";
 }
}
