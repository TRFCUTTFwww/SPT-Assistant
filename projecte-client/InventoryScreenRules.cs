namespace ProjectE.Client;
internal static class InventoryScreenRules {
 internal static bool Stale(bool local,bool alive,bool inventoryOpened,bool inventoryVisible,bool battleVisible,bool inventoryScreen,bool cursor,bool modal,bool nativeBusy)=>local&&alive&&inventoryOpened&&!inventoryVisible&&!battleVisible&&inventoryScreen&&!cursor&&!modal&&!nativeBusy;
 internal static bool Stable(long player,long expectedPlayer,long world,long expectedWorld,float seconds)=>player!=0&&player==expectedPlayer&&world!=0&&world==expectedWorld&&seconds>=1.5f;
}
