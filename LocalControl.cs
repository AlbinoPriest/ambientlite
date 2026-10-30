using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AmbientLite.Control;

sealed class LocalControl : IDisposable {
 readonly CancellationTokenSource stop=new CancellationTokenSource();readonly Func<Request,Task<Reply>> handle;
 NamedPipeServerStream active;readonly object gate=new object();readonly Task listening;readonly string name;
 public LocalControl(Func<Request,Task<Reply>> handle,string name=null){this.handle=handle;this.name=name??Wire.PipeName;listening=Run();}
 async Task Run(){
  while(!stop.IsCancellationRequested){try{
   var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
   security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
   security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
   using(var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,Wire.Limit,Wire.Limit,security)) {
    lock(gate){if(stop.IsCancellationRequested)return;active=pipe;}
    await pipe.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);
    using(var budget=new CancellationTokenSource(5000))using(budget.Token.Register(()=>pipe.Dispose())){
     var request=await Wire.Receive<Request>(pipe,budget.Token).ConfigureAwait(false);Reply reply;
     try{reply=request==null||request.Version!=1?new Reply{Error="Unsupported control version. Update both apps."}:await handle(request).ConfigureAwait(false);}catch(Exception e){reply=new Reply{Error=e.GetBaseException().Message};}
     await Wire.Send(pipe,reply,budget.Token).ConfigureAwait(false);
    }
   }
  }catch(Exception e){if(stop.IsCancellationRequested)return;LifecycleTrace.Write("Local control: "+e.Message);}
  if(!stop.IsCancellationRequested)await Task.Delay(25).ConfigureAwait(false);
  }
 }
 public void Dispose(){stop.Cancel();lock(gate){if(active!=null)active.Dispose();}}
}

sealed partial class LightWindow {
 Task remoteStart;
 internal bool Companion {get{return Environment.GetCommandLineArgs().Contains("--companion");}}
 internal LocalControl StartLocalControl(){return new LocalControl(r=>window.Dispatcher.InvokeAsync(()=>Control(r)).Task.Unwrap());}
 Options ControlOptions(){return new Options{Mode=liveMode==1?"static":"screen",Colour="#"+liveColour.ToString("X6"),Monitor=screens[monitor.SelectedIndex].DeviceName,Brightness=liveBrightness,Smoothing=liveSmoothing,Rate=liveRate,Top=Int32.Parse(topCount.Text),Side=Int32.Parse(sideCount.Text),Reverse=reverse.IsChecked==true,Compatibility=compatibilityCapture.IsChecked==true,OffOnExit=turnOffOnExit,StartInTray=startInTray,StartLighting=startLighting,StartWithWindows=StartupEnabled()};}
 static bool StartupEnabled(){using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistration.Key))return key!=null&&key.GetValue("AmbientLite")!=null;}
 static string Key(Options options){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Wire.Encode(options))).Replace("-","");}
 Reply Snapshot(){var options=ControlOptions();return new Reply{Ok=true,AppVersion=AppEnvironment.Version,ProcessId=System.Diagnostics.Process.GetCurrentProcess().Id,ScreenFrames=Interlocked.CompareExchange(ref frameCount,0,0),Running=IsRunning,Companion=Companion,Title=Get<TextBlock>("StateTitle").Text,Detail=Get<TextBlock>("StateDetail").Text,Settings=options,SettingsKey=Key(options),Displays=screens.Select((s,i)=>new Display{Id=s.DeviceName,Name="Screen "+(i+1)+" · "+s.Bounds.Width+" × "+s.Bounds.Height+(s.Primary?" · Main display":"")}).ToArray()};}
 internal static void ValidateControlOptions(Options o){
  if(o==null||(o.Mode!="screen"&&o.Mode!="static")||o.Colour==null||!System.Text.RegularExpressions.Regex.IsMatch(o.Colour,"^#[0-9A-Fa-f]{6}$")||o.Monitor==null||o.Monitor.Length>128||o.Brightness<0||o.Brightness>100||o.Smoothing<0||o.Smoothing>95||o.Rate<5||o.Rate>60||o.Top<1||o.Side<1||o.Top+2L*o.Side>254)throw new ArgumentException("Check colour, screen, brightness, smoothing, refresh rate and strip layout.");
 }
 async Task<Reply> Control(Request r){
  if(exiting)throw new IOException("AmbientLite is closing.");
  if(r.Command=="status")return Snapshot();
  if(r.Command=="apply"){
   ValidateControlOptions(r.Settings);var o=r.Settings;int index=Array.FindIndex(screens,s=>s.DeviceName==o.Monitor);
   if(index<0)throw new IOException("That display is no longer available. Refresh the display list.");
   var current=ControlOptions();if(r.ExpectedKey!=Key(current))throw new IOException("AmbientLite settings changed elsewhere. Refresh before applying.");
   if(IsRunning&&(o.Monitor!=current.Monitor||o.Top!=current.Top||o.Side!=current.Side||o.Reverse!=current.Reverse||o.Compatibility!=current.Compatibility))throw new IOException("Pause ambient lighting before changing the screen or strip setup.");
   if(o.StartWithWindows!=current.StartWithWindows)StartupRegistration.SetEnabled(o.StartWithWindows);
   loading=true;try{monitor.SelectedIndex=index;topCount.Text=o.Top.ToString();sideCount.Text=o.Side.ToString();reverse.IsChecked=o.Reverse;compatibilityCapture.IsChecked=o.Compatibility;brightness.Value=o.Brightness;smoothing.Value=o.Smoothing;rate.Value=o.Rate;turnOffOnExit=o.OffOnExit;startInTray=o.StartInTray;startLighting=o.StartLighting;offAtExit.IsChecked=o.OffOnExit;trayAtStartup.IsChecked=o.StartInTray;lightingAtStartup.IsChecked=o.StartLighting;ChooseColour((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(o.Colour),null);Navigate(o.Mode=="static"?"Colour":"Lighting");}finally{loading=false;}
   SaveIfValid();return Snapshot();
  }
  if(r.Command=="start") {if(!IsRunning){remoteStart=Start(false);await Task.WhenAny(remoteStart,Task.Delay(250));if(remoteStart.IsCompleted)await remoteStart;}return Snapshot();}
  if(r.Command=="pause"){await Stop();return Snapshot();}
  if(r.Command=="off"){await Stop();await SendLightsOff(configuredLights,CancellationToken.None);State(false,false,"Ambient lighting is off","Start lighting to resume your saved mode.");return Snapshot();}
  if(r.Command=="test"){if(IsRunning)throw new IOException("Pause lighting before testing the strip.");await Start(true);return Snapshot();}
  if(r.Command=="show"){Show();return Snapshot();}
  if(r.Command=="quit"){remoteStart=Task.Delay(100).ContinueWith(t=>RequestExit());return Snapshot();}
  throw new ArgumentException("Unknown AmbientLite control command.");
 }
}
