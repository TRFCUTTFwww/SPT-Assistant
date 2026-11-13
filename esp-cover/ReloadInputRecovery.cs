using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ReloadInputRecovery {
 static bool pending;
 static string status="not armed";
 internal static void Arm(){pending=true;status="waiting for gameplay";}
 internal static void BeforeReload(){
  // The host captures cursor state after PrepareForReload. Close owned UI
  // before that snapshot, so unloading cannot leave it pointing at a dead menu.
  InterfaceMenu.Close();RaidChat.CloseForReload();WorldEditor.Suspend();
 }
 internal static void Tick(){
  if(!pending||!Application.isFocused||!RaidClock.Active||Time.timeScale<=0||InterfaceMenu.InputBlocked||InterfaceMenu.Open||RaidChat.Open||WorldEditor.ModalOpen||DirectorStage.FreeActive)return;
  string? screen=EFT.UI.Screens.EftScreenManager.Instance?.CurrentScreenController?.ScreenType.ToString();
  // Inventory, pause, dialogue and loading screens retain native cursor/input.
  if(screen==null||!screen.Contains("Battle",StringComparison.OrdinalIgnoreCase))return;
  Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;Input.ResetInputAxes();pending=false;status="gameplay cursor restored";
 }
 internal static object Diagnostics()=>new{pending,status,scale=Time.timeScale,focused=Application.isFocused,cursor=Cursor.lockState.ToString(),visible=Cursor.visible,menu=InterfaceMenu.Open,blocked=InterfaceMenu.InputBlocked,chat=RaidChat.Open,editor=WorldEditor.ModalOpen,free=DirectorStage.FreeActive,screen=EFT.UI.Screens.EftScreenManager.Instance?.CurrentScreenController?.ScreenType.ToString()};
}
