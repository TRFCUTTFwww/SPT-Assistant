using Comfort.Common;
using EFT;
using EFT.UI;
using EFT.UI.Screens;
using Local.ModHost;
using UnityEngine;
namespace ProjectE.Client;

// InventoryScreen.Close only hides the view. Its owner must navigate away to
// restore BattleUI, IsInventoryOpened, input and the previous screen together.
internal sealed class InventoryScreenLifecycle(ModuleContext context) {
 float next,missingSince=-1,retryAt;IntPtr player,world;
 internal static bool Close(Player p,GamePlayerOwner? owner=null){
  if(!p.IsYourPlayer||!p.IsInventoryOpened||EftScreenManager.Instance?.CurrentScreenController?.ScreenType!=EEftScreenType.Inventory)return false;
  owner??=p.GetComponent<GamePlayerOwner>();
  if(owner==null||owner.Player?.Pointer!=p.Pointer)throw new InvalidOperationException("Local inventory screen owner is unavailable.");
  owner.CloseInventoryIfOpen();return true;
 }
 internal void Tick(bool modal){
  if(context.IsActive!=true||Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
  var current=Singleton<GameWorld>.Instance;var p=ProjectModule.CurrentRaidPlayer;
  if(p?.Pointer!=player||current?.Pointer!=world){player=p?.Pointer??IntPtr.Zero;world=current?.Pointer??IntPtr.Zero;missingSince=-1;}
  var ui=CommonUI.Instance;var screen=EftScreenManager.Instance?.CurrentScreenController;
  bool busy=p==null||p.ProcessStatus!=Player.EProcessStatus.None||p.ScheduledProcess!=null||p.HandsController==null||p.HandsController.Destroyed||p.HandsController.TryCast<Player.MedsController>()!=null;
  if(!InventoryScreenRules.Stale(p?.IsYourPlayer==true,p?.HealthController?.IsAlive==true,p?.IsInventoryOpened==true,ui?.InventoryScreen?.gameObject.activeInHierarchy==true,ui?.EftBattleUIScreen?.gameObject.activeInHierarchy==true,screen?.ScreenType==EEftScreenType.Inventory,Cursor.visible,modal,busy)){missingSince=-1;return;}
  if(missingSince<0)missingSince=Time.unscaledTime;
  if(Time.unscaledTime<retryAt||!InventoryScreenRules.Stable(p!.Pointer.ToInt64(),player.ToInt64(),current!.Pointer.ToInt64(),world.ToInt64(),Time.unscaledTime-missingSince))return;
  retryAt=Time.unscaledTime+5;
  try{if(Close(p)){missingSince=-1;context.Log(LogLevel.Warning,"Restored native BattleUI after stale hidden inventory screen; native screen navigation, inventory flag and input lifecycle restored together.");}}
  catch(Exception e){context.Log(LogLevel.Warning,"Inventory screen lifecycle recovery: "+e.Message);}
 }
}
