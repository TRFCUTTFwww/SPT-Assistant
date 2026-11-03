using System;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
internal static class AiHandsTransition {
 internal static void Empty(Player actor,Callback<IEmptyHandsController> callback,bool direct=false){
  if(direct){try{actor.FastForwardCurrentOperations();}catch{}actor.SetEmptyHands(callback);return;}
  try{actor.Proceed(false,callback,true);}catch(Exception error){Plugin.Self.Log.LogWarning("AI scheduled empty hands failed; retry direct SetEmptyHands: "+error);Empty(actor,callback,true);}
 }
}
