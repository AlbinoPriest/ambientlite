using System;
using System.Diagnostics;
using System.Drawing;

interface ILightingCapture : IDisposable {
 bool Grab(Rectangle bounds);
 Color[] Edge(int side,int top,bool reverse);
 void Pause();
}

// One capture session for the lifetime of a lighting worker. Static mode parks
// the session without polling it or repeatedly tearing down Desktop Duplication.
sealed class LightingFrames : IDisposable {
 readonly Func<ILightingCapture> create;readonly Rectangle bounds;readonly int top,side;readonly bool reverse;readonly Color[] last;
 ILightingCapture capture;
 public int LastSampleNonBlack{get;private set;}
 public LightingFrames(Func<ILightingCapture> create,Rectangle bounds,int top,int side,bool reverse){this.create=create;this.bounds=bounds;this.top=top;this.side=side;this.reverse=reverse;last=new Color[top+2*side];}
 public Color[] Read(int mode,int brightness,int smoothing,int rgb){
  Color[] colors;double power=brightness/100.0;
  if(mode==1){
   colors=new Color[last.Length];
   if(capture!=null)capture.Pause();
   var color=Color.FromArgb((int)(((rgb>>16)&255)*power),(int)(((rgb>>8)&255)*power),(int)((rgb&255)*power));
   for(int i=0;i<colors.Length;i++)colors[i]=color;
  }else{
   if(capture==null)capture=create();
   if(!capture.Grab(bounds))return null; // Keep the actual LED output until an image is ready.
   colors=capture.Edge(side,top,reverse);double blend=1-smoothing/100.0;int nonBlack=0;
   for(int i=0;i<colors.Length;i++){var c=colors[i];if((c.R|c.G|c.B)!=0)nonBlack++;colors[i]=Color.FromArgb((int)(last[i].R*(1-blend)+c.R*power*blend),(int)(last[i].G*(1-blend)+c.G*power*blend),(int)(last[i].B*(1-blend)+c.B*power*blend));}LastSampleNonBlack=nonBlack;
  }
  return colors;
 }
 public void Commit(Color[] colors){Array.Copy(colors,last,last.Length);}
 public void Dispose(){if(capture!=null){capture.Dispose();capture=null;}}
}

sealed class ReadbackWatchdog {
 readonly Func<long> milliseconds;long blockedAt=-1;
 public ReadbackWatchdog():this(Clock()){}
 static Func<long> Clock(){var clock=Stopwatch.StartNew();return ()=>clock.ElapsedMilliseconds;}
 internal ReadbackWatchdog(Func<long> milliseconds){this.milliseconds=milliseconds;}
 public void Ready(){blockedAt=-1;}
 public void Pause(){blockedAt=-1;}
 public bool Busy(){long now=milliseconds();if(blockedAt<0)blockedAt=now;return now-blockedAt>=2000;}
}
