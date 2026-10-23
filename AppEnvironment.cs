using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("AmbientLite")]
[assembly: AssemblyProduct("AmbientLite")]
[assembly: AssemblyCompany("AlbinoPriest")]
[assembly: AssemblyCopyright("Copyright © 2026 AlbinoPriest")]
[assembly: AssemblyVersion("0.2.1.0")]
[assembly: AssemblyFileVersion("0.2.1.0")]

static class AppEnvironment {
 public const string Version="0.2.1";
 public static readonly string Executable=Assembly.GetExecutingAssembly().Location;
 public static readonly bool Installed=File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installed.marker"));
 public static readonly string Settings=Path.Combine(Installed?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AmbientLite"):AppDomain.CurrentDomain.BaseDirectory,"settings.txt");
 public static void Save(string[] lines){
  Directory.CreateDirectory(Path.GetDirectoryName(Settings));
  string temporary=Settings+".tmp";
  File.WriteAllLines(temporary,lines);
  if(File.Exists(Settings))File.Replace(temporary,Settings,null);else File.Move(temporary,Settings);
 }
}

static class StartupRegistration {
 public const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
 public static string Command(string executable){
  string command="\""+Path.GetFullPath(executable)+"\" --startup";
  if(command.Length>260)throw new IOException("This folder path is too long for Windows startup. Move AmbientLite to a shorter path, or use the installer.");
  return command;
 }
 public static bool Enabled {get{using(var key=Registry.CurrentUser.OpenSubKey(Key))return key!=null&&string.Equals(key.GetValue("AmbientLite") as string,Command(AppEnvironment.Executable),StringComparison.OrdinalIgnoreCase);}}
 public static void SetEnabled(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(Key)){if(enabled)key.SetValue("AmbientLite",Command(AppEnvironment.Executable),RegistryValueKind.String);else key.DeleteValue("AmbientLite",false);}}
}

// Named events wake the UI only when another launch or the installer needs it.
sealed class AppSession : IDisposable {
 public const string MutexName=@"Local\AmbientLite.Robobloq";
 const string ShowEvent=@"Local\AmbientLite.Show",ExitEvent=@"Local\AmbientLite.Exit";
 EventWaitHandle show,exit; RegisteredWaitHandle showWait,exitWait;
 public AppSession(Action activate,Action quit):this(activate,quit,ShowEvent,ExitEvent){}
 internal AppSession(Action activate,Action quit,string showName,string exitName){
  show=new EventWaitHandle(false,EventResetMode.AutoReset,showName);exit=new EventWaitHandle(false,EventResetMode.AutoReset,exitName);
  showWait=ThreadPool.RegisterWaitForSingleObject(show,(s,t)=>activate(),null,Timeout.Infinite,false);
  exitWait=ThreadPool.RegisterWaitForSingleObject(exit,(s,t)=>quit(),null,Timeout.Infinite,false);
 }
 public static bool Activate(){return Signal(ShowEvent);}
 static bool Signal(string name){try{using(var handle=EventWaitHandle.OpenExisting(name)){handle.Set();return true;}}catch(WaitHandleCannotBeOpenedException){return false;}}
 public static int Shutdown(){return Shutdown(MutexName,ExitEvent,TimeSpan.FromSeconds(10));}
 internal static int Shutdown(string mutexName,string exitName,TimeSpan timeout){
  Mutex mutex;if(!Mutex.TryOpenExisting(mutexName,out mutex))return 0;
  using(mutex){
   var watch=Stopwatch.StartNew();
   // The instance mutex exists before WPF finishes loading and registers IPC.
   // Keep waiting for its handler; a missing event does not mean it has exited.
   while(!Signal(exitName)){
    if(WaitForExit(mutex,0))return 0;
    if(watch.Elapsed>=timeout)return 1;
    Thread.Sleep(25);
   }
   return WaitForExit(mutex,Math.Max(0,(int)(timeout-watch.Elapsed).TotalMilliseconds))?0:1;
  }
 }
 static bool WaitForExit(Mutex mutex,int milliseconds){try{if(!mutex.WaitOne(milliseconds))return false;}catch(AbandonedMutexException){}mutex.ReleaseMutex();return true;}
 public void Dispose(){showWait.Unregister(null);exitWait.Unregister(null);show.Dispose();exit.Dispose();}
}
