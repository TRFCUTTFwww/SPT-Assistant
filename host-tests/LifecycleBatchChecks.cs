using LocalSpt.Shared;

internal static class LifecycleBatchChecks {
 internal static void Run(){
  foreach(int size in new[]{1,2,20}){
   int created=0,executed=0;bool disposed=false;
   IEnumerable<Action> Steps(){
    try{
     yield return ()=>created=1;
     if(created!=1)throw new Exception("Enumerated plugin steps before creating plugin");
     for(int i=0;i<45;i++){
      if(executed!=i)throw new Exception("Enumerated ahead of dependent action");
      yield return ()=>executed++;
     }
    }finally{disposed=true;}
   }
   int batches=0;
   foreach(var batch in LifecycleSteps.Batch(Steps(),size)){batch();batches++;}
   if(executed!=45||batches!=(46+size-1)/size||!disposed)throw new Exception("Batch count, execution or disposal failed");
  }
  bool after=false;
  IEnumerable<Action> Failing(){yield return ()=>throw new InvalidOperationException("expected");after=true;yield return ()=>{};}
  try{foreach(var batch in LifecycleSteps.Batch(Failing(),20))batch();throw new Exception("Failure swallowed");}
  catch(InvalidOperationException e) when(e.Message=="expected"){}
  if(after)throw new Exception("Advanced lifecycle after failure");
  Console.WriteLine("PASS lifecycle batching: dependent initialization, batch boundaries, disposal and failure ordering");
 }
}
