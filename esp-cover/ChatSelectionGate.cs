using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
// Each social network can have one notification-driven selection in progress.
internal sealed class ChatSelectionGate {
 readonly HashSet<IntPtr> active=new();
 internal int Count=>active.Count;
 internal bool Enter(IntPtr network)=>active.Add(network);
 internal void Exit(IntPtr network)=>active.Remove(network);
}
