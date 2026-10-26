using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

static class SessionTests {
 static string Name(string prefix,string suffix){return prefix+"."+suffix;}
 static Process Child(params string[] args){return Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,string.Join(" ",args)){UseShellExecute=false,CreateNoWindow=true});}
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static int Server(string prefix){
  using(var ready=EventWaitHandle.OpenExisting(Name(prefix,"Ready")))
  using(var gate=EventWaitHandle.OpenExisting(Name(prefix,"Gate")))
  using(var abort=EventWaitHandle.OpenExisting(Name(prefix,"Abort")))
  using(var mutex=new Mutex(true,Name(prefix,"Mutex")))
  using(var quit=new ManualResetEvent(false)){
   try{
    ready.Set();
    if(WaitHandle.WaitAny(new WaitHandle[]{gate,abort},15000)!=0)return 2;
    using(var session=new AppSession(()=>{},()=>quit.Set(),Name(prefix,"Show"),Name(prefix,"Exit")))
     return WaitHandle.WaitAny(new WaitHandle[]{quit,abort},15000)==0?0:2;
   }finally{mutex.ReleaseMutex();}
  }
 }
 static void DelayedHandler(bool expectTimeout){
  string prefix=@"Local\AmbientLite.SessionTest."+Guid.NewGuid().ToString("N");
  using(var ready=new EventWaitHandle(false,EventResetMode.AutoReset,Name(prefix,"Ready")))
  using(var gate=new EventWaitHandle(false,EventResetMode.ManualReset,Name(prefix,"Gate")))
  using(var abort=new EventWaitHandle(false,EventResetMode.ManualReset,Name(prefix,"Abort")))
  using(var stopping=new EventWaitHandle(false,EventResetMode.AutoReset,Name(prefix,"Stopping")))
  using(var server=Child("--server",prefix)){
   try{
    Assert(ready.WaitOne(10000),"Test process did not acquire its instance mutex.");
    using(var stop=Child("--stop",prefix,expectTimeout?"500":"10000")){
     Assert(stopping.WaitOne(10000),"Shutdown test client did not start.");
     if(expectTimeout){
      Assert(stop.WaitForExit(5000)&&stop.ExitCode==1,"An unresponsive starting instance was incorrectly reported as stopped.");
      Assert(!server.HasExited,"The starting instance should still be running.");
     }else{
      Assert(!stop.WaitForExit(300),"Shutdown returned before the starting instance registered its handler.");
      gate.Set();
      Assert(stop.WaitForExit(10000)&&stop.ExitCode==0,"Shutdown did not wait for the delayed handler and finish successfully.");
      Assert(server.WaitForExit(5000)&&server.ExitCode==0,"Shutdown did not stop the delayed instance.");
     }
    }
   }finally{abort.Set();Assert(server.WaitForExit(5000),"Test process did not clean up.");}
  }
 }
 static int Main(string[] args){try{
  if(args.Length>0&&args[0]=="--server")return Server(args[1]);
  if(args.Length>0&&args[0]=="--stop"){
   using(var stopping=EventWaitHandle.OpenExisting(Name(args[1],"Stopping")))stopping.Set();
   return AppSession.Shutdown(Name(args[1],"Mutex"),Name(args[1],"Exit"),TimeSpan.FromMilliseconds(int.Parse(args[2])));
  }
  DelayedHandler(false);DelayedHandler(true);
  ShutdownTests.Run();
  LightingTests.Run();
  string empty=@"Local\AmbientLite.SessionTest."+Guid.NewGuid().ToString("N");
  Assert(AppSession.Shutdown(Name(empty,"Mutex"),Name(empty,"Exit"),TimeSpan.FromSeconds(1))==0,"Stopping an absent instance should succeed.");
  Console.WriteLine("Delayed startup shutdown, timeout, and absent instance checks passed.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
}
