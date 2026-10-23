using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

// Original implementation. Protocol facts derived from the installed SyncLight app.
static class Native {
 [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
 [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h,out IntPtr p);
 [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr p);
 [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr p,IntPtr caps);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr SetupDiGetClassDevs(ref Guid g,string e,IntPtr w,uint flags);
 [StructLayout(LayoutKind.Sequential)] public struct Interface {public int size;public Guid guid;public int flags;public IntPtr reserved;}
 [DllImport("setupapi.dll",SetLastError=true)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr dev,ref Guid guid,uint i,ref Interface data);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref Interface data,IntPtr detail,int size,out int needed,IntPtr dev);
 [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern SafeFileHandle CreateFile(string p,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr w);
 [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr w,IntPtr dc);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
 [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr dc,int mode);
 [DllImport("gdi32.dll",SetLastError=true)] public static extern bool StretchBlt(IntPtr dst,int x,int y,int w,int h,IntPtr src,int sx,int sy,int sw,int sh,uint op);
 [StructLayout(LayoutKind.Sequential)] public struct BitmapInfo {public uint size;public int width,height;public ushort planes,bits;public uint compression,imageSize;public int xp,yp;public uint used,important;}
 [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
}

sealed class Hid : IDisposable {
 FileStream stream; public string Path; public int ReportSize;
 public static string[] Find() {
  Guid g;Native.HidD_GetHidGuid(out g);var set=Native.SetupDiGetClassDevs(ref g,null,IntPtr.Zero,18);var result=new System.Collections.Generic.List<string>();
  if(set==new IntPtr(-1))throw new Win32Exception();
  try {for(uint i=0;;i++){var d=new Native.Interface();d.size=Marshal.SizeOf(d);if(!Native.SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref g,i,ref d))break;
   int needed;Native.SetupDiGetDeviceInterfaceDetail(set,ref d,IntPtr.Zero,0,out needed,IntPtr.Zero);if(needed<=0)continue;
   IntPtr p=Marshal.AllocHGlobal(needed);try{Marshal.WriteInt32(p,IntPtr.Size==8?8:6);if(Native.SetupDiGetDeviceInterfaceDetail(set,ref d,p,needed,out needed,IntPtr.Zero)){string s=Marshal.PtrToStringUni(IntPtr.Add(p,4));if(s.ToLowerInvariant().Contains("vid_1a86&pid_fe07&mi_00"))result.Add(s);}}finally{Marshal.FreeHGlobal(p);}
  }}finally{Native.SetupDiDestroyDeviceInfoList(set);}return result.ToArray();
 }
 public Hid(string path) {
  Path=path;var h=Native.CreateFile(path,0x40000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);if(h.IsInvalid){h.Dispose();throw new Win32Exception();}
  try {IntPtr p;if(!Native.HidD_GetPreparsedData(h,out p))throw new Win32Exception();try{IntPtr caps=Marshal.AllocHGlobal(64);try{if(Native.HidP_GetCaps(p,caps)!=0x110000)throw new IOException("Cannot read HID capabilities");ReportSize=(ushort)Marshal.ReadInt16(caps,6);}finally{Marshal.FreeHGlobal(caps);}}finally{Native.HidD_FreePreparsedData(p);}
   if(ReportSize!=65)throw new IOException("Unexpected HID output report length: "+ReportSize+" (expected 65)");stream=new FileStream(h,FileAccess.Write,65,true);
  }catch{h.Dispose();throw;}
 }
 public async Task Send(byte[] frame,CancellationToken token) {
  byte[] report=new byte[65];for(int p=0;p<frame.Length;p+=64){Array.Clear(report,0,65);Buffer.BlockCopy(frame,p,report,1,Math.Min(64,frame.Length-p));using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(1000);await stream.WriteAsync(report,0,65,timeout.Token).ConfigureAwait(false);}}
 }
 public void Dispose(){if(stream!=null)stream.Dispose();}
}

sealed class Capture : IDisposable {
 public const int W=256,H=128;IntPtr screen,dc,bmp,old,bits;byte[] pixels=new byte[W*H*4];
 public Capture(){screen=Native.GetDC(IntPtr.Zero);dc=Native.CreateCompatibleDC(screen);var info=new Native.BitmapInfo{size=40,width=W,height=-H,planes=1,bits=32};bmp=Native.CreateDIBSection(screen,ref info,0,out bits,IntPtr.Zero,0);if(screen==IntPtr.Zero||dc==IntPtr.Zero||bmp==IntPtr.Zero){Dispose();throw new IOException("Cannot create capture surface");}old=Native.SelectObject(dc,bmp);Native.SetStretchBltMode(dc,3);}
 public void Grab(Rectangle r){if(!Native.StretchBlt(dc,0,0,W,H,screen,r.X,r.Y,r.Width,r.Height,0x40CC0020))throw new Win32Exception("Screen capture failed; try the normal desktop");Marshal.Copy(bits,pixels,0,pixels.Length);}
 public Color Average(int x,int y,int w,int h){int rr=0,g=0,b=0,n=0;for(int yy=y;yy<Math.Min(H,y+h);yy++)for(int xx=x;xx<Math.Min(W,x+w);xx++){int p=(yy*W+xx)*4;b+=pixels[p];g+=pixels[p+1];rr+=pixels[p+2];n++;}return Color.FromArgb(rr/Math.Max(1,n),g/Math.Max(1,n),b/Math.Max(1,n));}
 public Color[] Edge(int side,int top,bool reverse){var a=new Color[side*2+top];for(int i=0;i<side;i++){int y=H-(i+1)*H/side;int hh=Math.Max(1,H/side);a[i]=Average(reverse?W-20:0,y,20,hh);a[side+top+i]=Average(reverse?0:W-20,i*H/side,20,hh);}for(int i=0;i<top;i++){int x=(reverse?top-1-i:i)*W/top;a[side+i]=Average(x,0,Math.Max(1,W/top),12);}return a;}
 public void Dispose(){if(old!=IntPtr.Zero&&dc!=IntPtr.Zero)Native.SelectObject(dc,old);if(bmp!=IntPtr.Zero)Native.DeleteObject(bmp);if(dc!=IntPtr.Zero)Native.DeleteDC(dc);if(screen!=IntPtr.Zero)Native.ReleaseDC(IntPtr.Zero,screen);bmp=dc=screen=IntPtr.Zero;}
}

static class Protocol {
 public static byte[] Frame(Color[] colors,int sequence){if(colors.Length<1||colors.Length>254)throw new ArgumentException("LED count must be 1..254");byte[] b=new byte[7+colors.Length*5];b[0]=83;b[1]=67;b[2]=(byte)(b.Length>>8);b[3]=(byte)b.Length;b[4]=(byte)sequence;b[5]=128;for(int i=0;i<colors.Length;i++){int p=6+i*5;b[p]=(byte)(i+1);b[p+1]=colors[i].R;b[p+2]=colors[i].G;b[p+3]=colors[i].B;b[p+4]=(byte)(i+1);}int sum=0;for(int i=0;i<b.Length-1;i++)sum+=b[i];b[b.Length-1]=(byte)sum;return b;}
}

static class Program {
 [STAThread] static int Main(string[] args){Native.SetProcessDPIAware();LifecycleTrace.Write("Main entered");try{
  if(args.Contains("--shutdown"))return AppSession.Shutdown();
  if(args.Contains("--licenses")){foreach(string name in new[]{"Ambient.License","Ambient.SwooshLicense"})using(var reader=new StreamReader(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name)))Console.WriteLine(reader.ReadToEnd());return 0;}
  if(args.Contains("--startup-enable")||args.Contains("--startup-disable")){StartupRegistration.SetEnabled(args.Contains("--startup-enable"));return 0;}
  if(args.Contains("--probe")){var paths=Hid.Find();foreach(var p in paths)using(var hid=new Hid(p))Console.WriteLine("Robobloq VID 1A86 PID FE07 MI00; output report "+hid.ReportSize+" bytes");return paths.Length==1?0:1;}
  if(args.Contains("--self-test")){var b=Protocol.Frame(new[]{Color.Red,Color.Blue},2);string hex=BitConverter.ToString(b);if(hex!="53-43-00-11-02-80-01-FF-00-00-01-02-00-00-FF-02-2D")throw new Exception("Packet mismatch: "+hex);if(ColourMath.FromHsv(0,1,1)!=System.Windows.Media.Colors.Red||ColourMath.FromHsv(120,1,1)!=System.Windows.Media.Colors.Lime||ColourMath.FromHsv(240,1,1)!=System.Windows.Media.Colors.Blue||ColourMath.FromHsv(70,0,1)!=System.Windows.Media.Colors.White)throw new Exception("Colour conversion test failed");double hue,sat;ColourMath.ToHsv(System.Windows.Media.Colors.Cyan,out hue,out sat);if(Math.Abs(hue-180)>0.001||Math.Abs(sat-1)>0.001)throw new Exception("Colour selection test failed");Console.WriteLine("Protocol and colour conversion tests passed");return 0;}
  if(args.Contains("--blue-test")){if(Process.GetProcessesByName("SyncLight").Length>0)throw new Exception("Close SyncLight first");var paths=Hid.Find();if(paths.Length!=1)throw new Exception("Expected one controller");using(var hid=new Hid(paths[0]))hid.Send(Protocol.Frame(Enumerable.Repeat(Color.FromArgb(0,0,40),71).ToArray(),2),CancellationToken.None).GetAwaiter().GetResult();Console.WriteLine("Dim blue frame sent to all 71 LEDs");return 0;}
  if(args.Contains("--benchmark")||args.Contains("--hardware-benchmark")){bool send=args.Contains("--hardware-benchmark");if(send&&Process.GetProcessesByName("SyncLight").Length>0)throw new Exception("Close SyncLight first");Hid hid=null;if(send){var paths=Hid.Find();if(paths.Length!=1)throw new Exception("Expected one controller");hid=new Hid(paths[0]);}try{var screen=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName.EndsWith("DISPLAY2"))??Screen.PrimaryScreen;var proc=Process.GetCurrentProcess();var cpu=proc.TotalProcessorTime;var watch=Stopwatch.StartNew();int frames=0;using(var c=new Capture()){while(watch.Elapsed.TotalSeconds<20){var frame=Stopwatch.StartNew();c.Grab(screen.Bounds);var packet=Protocol.Frame(c.Edge(15,41,true),frames%254+1);if(hid!=null)hid.Send(packet,CancellationToken.None).GetAwaiter().GetResult();frames++;Thread.Sleep(Math.Max(1,50-(int)frame.ElapsedMilliseconds));}}proc.Refresh();Console.WriteLine("Frames="+frames+" USB="+send+" CPU="+((proc.TotalProcessorTime-cpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F2")+"% WorkingSetMB="+(proc.WorkingSet64/1048576.0).ToString("F1"));return 0;}finally{if(hid!=null)hid.Dispose();}}
  int renderAt=Array.IndexOf(args,"--render-docs");if(renderAt>=0){if(renderAt+1>=args.Length)throw new ArgumentException("Provide an image output directory");var app=new System.Windows.Application();var ui=new LightWindow();ui.Window.Loaded+=(sender,e)=>ui.Window.Dispatcher.BeginInvoke(new Action(()=>ui.RenderDocumentation(args[renderAt+1])),System.Windows.Threading.DispatcherPriority.ContextIdle);app.Run(ui.Window);return 0;}
  bool created;using(var mutex=new Mutex(true,AppSession.MutexName,out created)){if(!created){if(!args.Contains("--startup")){if(!AppSession.Activate())MessageBox.Show("AmbientLite is already running. Look in the tray.");}return 0;}try{LifecycleTrace.Write("Instance mutex acquired; creating UI");var app=new System.Windows.Application();app.ShutdownMode=System.Windows.ShutdownMode.OnMainWindowClose;var ui=new LightWindow();LifecycleTrace.Write("UI created");using(var session=new AppSession(ui.Activate,ui.RequestExit))app.Run(ui.Window);LifecycleTrace.Write("UI loop exited");}finally{mutex.ReleaseMutex();}}return 0;
 }catch(Exception ex){LifecycleTrace.Write(ex.ToString());Console.Error.WriteLine(ex);return 1;}}
}
