using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
// Each social network can have one notification-driven selection in progress.
internal sealed class ChatSelectionGate {
 readonly HashSet<IntPtr> active=new();
 internal int Count=>active.Count;
 internal bool Enter(IntPtr network)=>active.Add(network);
 internal void Exit(IntPtr network)=>active.Remove(network);
 internal bool Select<T>(IntPtr network,T? previous,T? next,Action<T?> assign,Action<T,bool> mark,Action<T?> notify) where T:class {
  if(!Enter(network))return false;
  try{
   // Every callback (including row disposal and deferred row creation) must
   // observe the new selection. Blocking only nested calls leaves stale views.
   assign(next);
   if(previous!=null)mark(previous,false);
   notify(next);
   if(next!=null)mark(next,true);
   return true;
  }finally{Exit(network);}
 }
}
