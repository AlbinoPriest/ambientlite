using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Drawing=System.Drawing;

sealed partial class LightWindow {
 bool turnOffOnExit=true;int configuredLights=71;CheckBox offAtExit;Task<bool> exitLighting;
 void SetupShutdown(){
  offAtExit=Get<CheckBox>("OffAtExit");offAtExit.IsChecked=turnOffOnExit;
  offAtExit.Checked+=(s,e)=>{turnOffOnExit=true;SaveIfValid();};offAtExit.Unchecked+=(s,e)=>{turnOffOnExit=false;SaveIfValid();};
  if(!renderingDocumentation)Application.Current.SessionEnding+=(s,e)=>PrepareForWindowsShutdown();
 }
 Task<bool> PrepareExitLighting(){
  if(exitLighting!=null)return exitLighting;
  var source=cancellation;var running=worker;int count=configuredLights;
  Func<CancellationToken,Task> blackout=turnOffOnExit?(Func<CancellationToken,Task>)(token=>SendLightsOff(count,token)):null;
  exitLighting=ShutdownLighting.RunAsync(running,()=>{if(source!=null)source.Cancel();},blackout,ShutdownLighting.BudgetMilliseconds);
  return exitLighting;
 }
 static async Task SendLightsOff(int count,CancellationToken token){
  token.ThrowIfCancellationRequested();
  if(Process.GetProcessesByName("SyncLight").Length>0)throw new IOException("SyncLight is still running; leaving its output alone.");
  var paths=Hid.Find();if(paths.Length!=1)throw new IOException("A single compatible controller is not connected.");
  token.ThrowIfCancellationRequested();
  using(var hid=new Hid(paths[0])){
   var black=Enumerable.Repeat(Drawing.Color.Black,count).ToArray();
   // Two different sequence numbers also cover a controller that ignores a repeat.
   await hid.Send(Protocol.Frame(black,253),token).ConfigureAwait(false);
   await hid.Send(Protocol.Frame(black,254),token).ConfigureAwait(false);
  }
  LifecycleTrace.Write("Lights-off frames sent for "+count+" LEDs");
 }
 void PrepareForWindowsShutdown(){
  LifecycleTrace.Write("Windows session ending");exiting=true;HideQuick();SaveIfValid();
  // The worker's pending dispatcher operation is cancellable; this bounded wait
  // does not need the blocked UI thread to finish capture or send the black frame.
  try{PrepareExitLighting().GetAwaiter().GetResult();}catch(Exception ex){LifecycleTrace.Write("Session cleanup: "+ex.Message);}
 }
 public void RequestSessionEndTest(){window.Dispatcher.BeginInvoke(new Action(()=>{PrepareForWindowsShutdown();window.Close();}),DispatcherPriority.ContextIdle);}
}
