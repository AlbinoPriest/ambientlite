using System;
using System.Threading;
using System.Threading.Tasks;

// The Windows session-ending callback cannot await the UI dispatcher. Stop and
// USB cleanup run independently, with a deadline so lighting never holds up Windows.
static class ShutdownLighting {
 public const int BudgetMilliseconds=1500;
 public static async Task<bool> RunAsync(Task worker,Action cancel,Func<CancellationToken,Task> blackout,int budgetMilliseconds){
  using(var deadline=new CancellationTokenSource()){
   var token=deadline.Token;deadline.CancelAfter(budgetMilliseconds);
   var cleanup=Task.Run(async()=>{
    try{
     if(cancel!=null)try{cancel();}catch(ObjectDisposedException){}
     if(worker!=null)try{await worker.ConfigureAwait(false);}catch(Exception ex){LifecycleTrace.Write("Lighting worker ended: "+ex.Message);}
     token.ThrowIfCancellationRequested();
     if(blackout!=null)await blackout(token).ConfigureAwait(false);
     return true;
    }catch(Exception ex){LifecycleTrace.Write("Exit lighting cleanup: "+ex.Message);return false;}
   });
   if(await Task.WhenAny(cleanup,Task.Delay(budgetMilliseconds)).ConfigureAwait(false)!=cleanup){
    deadline.Cancel();LifecycleTrace.Write("Exit lighting cleanup reached its deadline");return false;
   }
   return await cleanup.ConfigureAwait(false);
  }
 }
}
