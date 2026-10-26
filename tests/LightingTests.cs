using System;
using System.Drawing;
using System.Linq;

static class LightingTests {
 sealed class FakeCapture : ILightingCapture {
  public bool Ready;public Color Color=Color.Blue;public int Reads,Pauses,Disposals;
  public bool Grab(Rectangle bounds){Reads++;return Ready;}
  public Color[] Edge(int side,int top,bool reverse){return Enumerable.Repeat(Color,top+2*side).ToArray();}
  public void Pause(){Pauses++;}
  public void Dispose(){Disposals++;}
 }
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){
  var fake=new FakeCapture();int created=0;
  using(var frames=new LightingFrames(()=>{created++;return fake;},new Rectangle(0,0,3440,1440),2,1,true)){
   var orange=frames.Read(1,100,50,0xFF6400);frames.Commit(orange);
   Assert(created==0,"Static startup created a capture session.");
   Assert(frames.Read(0,100,50,0)==null,"Uninitialized capture produced a black frame instead of waiting.");
   fake.Ready=true;var blended=frames.Read(0,100,50,0);
   Assert(blended[0].R==127&&blended[0].G==50&&blended[0].B==127,"Screen mode did not blend from the last static LED colour.");frames.Commit(blended);
   for(int i=0;i<100;i++){
    int reads=fake.Reads;var solid=frames.Read(1,100,0,0xFF6400);frames.Commit(solid);
    Assert(fake.Reads==reads,"Static mode polled the desktop.");
    var screen=frames.Read(0,100,0,0);Assert(screen.All(c=>c.ToArgb()==Color.Blue.ToArgb()),"Mode switch lost the capture image.");frames.Commit(screen);
   }
   Assert(created==1&&fake.Disposals==0,"Mode switches destroyed and recreated the capture session.");
   fake.Color=Color.Black;var black=frames.Read(0,100,0,0);
   Assert(black!=null&&black.All(c=>c.R==0&&c.G==0&&c.B==0),"A valid black screen was incorrectly rejected.");
  }
  Assert(fake.Disposals==1,"Capture was not released on worker shutdown.");
  long now=0;var watchdog=new ReadbackWatchdog(()=>now);
  Assert(!watchdog.Busy(),"Readback was considered stalled immediately.");now=1999;Assert(!watchdog.Busy(),"Watchdog fired too early.");now=2000;Assert(watchdog.Busy(),"Stalled GPU readback was not detected.");
  watchdog.Ready();Assert(!watchdog.Busy(),"Completed readback did not reset the watchdog.");
  watchdog.Pause();now=100000;Assert(!watchdog.Busy(),"Static idle time was mistaken for a stuck GPU.");
  Console.WriteLine("100 mode round trips, static idle, warmup, smoothing continuity, valid black images, and readback recovery checks passed.");
 }
}
