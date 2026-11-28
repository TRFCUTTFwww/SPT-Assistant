using System;
using System.Collections.Generic;

namespace LocalSpt.Shared;

internal static class LifecycleSteps {
 internal static IEnumerable<Action> Batch(IEnumerable<Action> steps,int batchSize){
  if(batchSize<1)throw new ArgumentOutOfRangeException(nameof(batchSize));
  using var iterator=steps.GetEnumerator();
  while(iterator.MoveNext()){
   var first=iterator.Current;
   yield return ()=>{
    first();
    // A later step's iterator may depend on state created by the previous action.
    // Never enumerate ahead of execution, even within the same batch.
    for(int i=1;i<batchSize&&iterator.MoveNext();i++)iterator.Current();
   };
  }
 }
}
