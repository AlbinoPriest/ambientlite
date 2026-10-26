using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

static class CaptureDiagnostics {
 public static int Switches(){
  var screen=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName.EndsWith("DISPLAY2"))??Screen.PrimaryScreen;int created=0,minLit=71;Capture capture=null;
  using(var frames=new LightingFrames(()=>{created++;return capture=new Capture();},screen.Bounds,41,15,true)){
   for(int cycle=0;cycle<30;cycle++){
    frames.Commit(frames.Read(1,100,45,0xFF6421));Thread.Sleep(30);
    var wait=Stopwatch.StartNew();System.Drawing.Color[] colors=null;
    while(colors==null&&wait.Elapsed.TotalSeconds<5){colors=frames.Read(0,100,45,0);if(colors==null)Thread.Sleep(10);}
    if(colors==null)throw new Exception("Screen capture did not resume in cycle "+cycle);
    frames.Commit(colors);minLit=Math.Min(minLit,frames.LastSampleNonBlack);Thread.Sleep(30);
   }
   Console.WriteLine("Real GPU mode round trips=30 CaptureSessions="+created+" MinimumNonBlackSampleZones="+minLit+" Readbacks="+capture.Readbacks+" BusySkips="+capture.BusySkips);
   if(created!=1||capture.Readbacks==0)throw new Exception("Capture session did not stay reusable.");
  }return 0;
 }
 public static int Run(string[] args){
  int seconds=10,at=Array.IndexOf(args,"--seconds"),requested;if(at>=0&&at+1<args.Length&&int.TryParse(args[at+1],out requested))seconds=Math.Max(1,Math.Min(60,requested));
  var screen=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName.EndsWith("DISPLAY2"))??Screen.PrimaryScreen;
  using(var capture=new Capture(args.Contains("--compatibility"))){
   var startup=Stopwatch.StartNew();capture.Grab(screen.Bounds);startup.Stop();
   var samples=new List<double>();int ready=0;using(var process=Process.GetCurrentProcess()){
    var cpu=process.TotalProcessorTime;var watch=Stopwatch.StartNew();
    while(watch.Elapsed.TotalSeconds<seconds){var tick=Stopwatch.StartNew();if(capture.Grab(screen.Bounds)){capture.Edge(15,41,true);ready++;}samples.Add(tick.Elapsed.TotalMilliseconds);Thread.Sleep(Math.Max(1,50-(int)tick.ElapsedMilliseconds));}
    samples.Sort();process.Refresh();
    Console.WriteLine("Backend="+capture.Backend+" Display="+screen.DeviceName+" Size="+screen.Bounds.Width+"x"+screen.Bounds.Height+" Seconds="+seconds+" Ready="+ready+" Readbacks="+capture.Readbacks+" BusySkips="+capture.BusySkips);
    Console.WriteLine("StartupMs="+startup.Elapsed.TotalMilliseconds.ToString("F2")+" MedianMs="+samples[samples.Count/2].ToString("F3")+" P95Ms="+samples[Math.Min(samples.Count-1,(int)(samples.Count*.95))].ToString("F3")+" MaxMs="+samples[samples.Count-1].ToString("F3")+" CPU="+((process.TotalProcessorTime-cpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F2")+"% WorkingSetMB="+(process.WorkingSet64/1048576.0).ToString("F1"));
   }
   if(ready==0)throw new Exception("No desktop image was read back.");
  }return 0;
 }
}
