using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

static class ShutdownTests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 sealed class NoUiPump : SynchronizationContext {public override void Post(SendOrPostCallback callback,object state){throw new Exception("Shutdown tried to use the blocked UI dispatcher.");}}
 public static void Run(){
  var old=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new NoUiPump());
  try{
   var worker=new TaskCompletionSource<bool>();int writes=0;bool cancelled=false;
   bool complete=ShutdownLighting.RunAsync(worker.Task,()=>{cancelled=true;worker.SetCanceled();},token=>{Assert(worker.Task.IsCompleted,"Blackout raced the lighting worker.");Interlocked.Increment(ref writes);return Task.FromResult(true);},1000).GetAwaiter().GetResult();
   Assert(complete&&cancelled&&writes==1,"Orderly shutdown did not cancel capture before blacking out.");

   var blocked=new TaskCompletionSource<bool>();int lateWrites=0;var watch=Stopwatch.StartNew();
   complete=ShutdownLighting.RunAsync(blocked.Task,()=>{},token=>{Interlocked.Increment(ref lateWrites);return Task.FromResult(true);},100).GetAwaiter().GetResult();
   Assert(!complete&&watch.Elapsed.TotalSeconds<3,"An unresponsive worker held up shutdown.");
   blocked.SetResult(true);Thread.Sleep(100);Assert(lateWrites==0,"Blackout ran after its deadline.");

   var failed=new TaskCompletionSource<bool>();failed.SetException(new Exception("capture device lost"));int recoveryWrites=0;
   complete=ShutdownLighting.RunAsync(failed.Task,null,token=>{Interlocked.Increment(ref recoveryWrites);return Task.FromResult(true);},1000).GetAwaiter().GetResult();
   Assert(complete&&recoveryWrites==1,"A failed capture worker prevented the off command.");

   bool outputCancelled=false;watch.Restart();
   complete=ShutdownLighting.RunAsync(null,null,async token=>{try{await Task.Delay(10000,token).ConfigureAwait(false);}catch(OperationCanceledException){outputCancelled=true;throw;}},100).GetAwaiter().GetResult();
   Assert(!complete&&watch.Elapsed.TotalSeconds<3,"An unresponsive device held up shutdown.");
   Thread.Sleep(100);Assert(outputCancelled,"Off command did not receive cancellation.");

   complete=ShutdownLighting.RunAsync(Task.FromResult(true),null,null,1000).GetAwaiter().GetResult();
   Assert(complete,"Opting out of blackout should still allow a clean exit.");
  }finally{SynchronizationContext.SetSynchronizationContext(old);}
  Console.WriteLine("Shutdown ordering, blocked-UI safety, deadlines, device cancellation, and opt-out checks passed.");
 }
}
