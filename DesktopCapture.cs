using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Forms=System.Windows.Forms;

// All D3D calls are serialized by the lighting worker. No COM RCWs or packages.
static class Dx {
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int EnumObject(IntPtr self,uint index,out IntPtr value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int OutputDescription(IntPtr self,out OutputDesc desc);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int Duplicate(IntPtr self,IntPtr device,out IntPtr value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int Acquire(IntPtr self,uint timeout,out FrameInfo info,out IntPtr resource);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int Result(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void TextureDescription(IntPtr self,out TextureDesc desc);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateTexture(IntPtr self,ref TextureDesc desc,IntPtr data,out IntPtr texture);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateView(IntPtr self,IntPtr texture,IntPtr desc,out IntPtr view);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateShader(IntPtr self,IntPtr bytes,UIntPtr size,IntPtr linkage,out IntPtr shader);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateSampler(IntPtr self,ref SamplerDesc desc,out IntPtr sampler);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Copy(IntPtr self,IntPtr destination,IntPtr source);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void SetResources(IntPtr self,uint start,uint count,[In] IntPtr[] resources);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void SetUav(IntPtr self,uint start,uint count,[In] IntPtr[] resources,IntPtr counts);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void SetShader(IntPtr self,IntPtr shader,IntPtr classes,uint count);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Dispatch(IntPtr self,uint x,uint y,uint z);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int Map(IntPtr self,IntPtr resource,uint subresource,uint type,uint flags,out Mapped data);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Unmap(IntPtr self,IntPtr resource,uint subresource);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Void(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate IntPtr BlobPointer(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate UIntPtr BlobSize(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int Priority(IntPtr self,int priority);
 [DllImport("dxgi.dll")] public static extern int CreateDXGIFactory1(ref Guid iid,out IntPtr factory);
 [DllImport("d3d11.dll")] public static extern int D3D11CreateDevice(IntPtr adapter,uint driver,IntPtr software,uint flags,IntPtr levels,uint levelCount,uint sdk,out IntPtr device,out uint level,out IntPtr context);
 [DllImport("d3dcompiler_47.dll",CallingConvention=CallingConvention.StdCall,CharSet=CharSet.Ansi)] public static extern int D3DCompile(byte[] source,UIntPtr size,string name,IntPtr defines,IntPtr include,string entry,string target,uint flags,uint flags2,out IntPtr code,out IntPtr errors);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct OutputDesc {[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;public int Left,Top,Right,Bottom;public int Attached;public uint Rotation;public IntPtr Monitor;}
 [StructLayout(LayoutKind.Sequential)] public struct FrameInfo {public long Present,MouseUpdate;public uint Accumulated,Coalesced,Protected;public int PointerX,PointerY,PointerVisible;public uint Metadata,Shape;}
 [StructLayout(LayoutKind.Sequential)] public struct TextureDesc {public uint Width,Height,Mips,Array,Format,SampleCount,SampleQuality,Usage,Bind,Cpu,Misc;}
 [StructLayout(LayoutKind.Sequential)] public struct Mapped {public IntPtr Data;public uint RowPitch,DepthPitch;}
 [StructLayout(LayoutKind.Sequential)] public struct SubresourceData {public IntPtr Data;public uint RowPitch,SlicePitch;}
 [StructLayout(LayoutKind.Sequential)] public struct SamplerDesc {public uint Filter,U,V,W;public float Bias;public uint Anisotropy,Comparison;public float R,G,B,A,MinLod,MaxLod;}
 public static T Method<T>(IntPtr self,int slot) where T:class {return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self),slot*IntPtr.Size),typeof(T)) as T;}
 public static IntPtr Query(IntPtr self,string iid){var guid=new Guid(iid);IntPtr value;Check(Marshal.QueryInterface(self,ref guid,out value),"open a graphics interface");return value;}
 public static void Free(ref IntPtr value){if(value!=IntPtr.Zero){Marshal.Release(value);value=IntPtr.Zero;}}
 public static void Check(int hr,string action){if(hr<0)throw new COMException("Smooth capture could not "+action+" (0x"+hr.ToString("X8")+"). Try Compatibility capture in Strip setup.",hr);}
 public static bool Recoverable(int hr){return hr==unchecked((int)0x887A0026)||hr==unchecked((int)0x887A0005)||hr==unchecked((int)0x887A0006)||hr==unchecked((int)0x887A0007)||hr==unchecked((int)0x80070005);}
}

sealed class DesktopCapture : IDisposable {
 const int Busy=unchecked((int)0x887A000A),NoFrame=unchecked((int)0x887A0027);
 IntPtr device,context,duplication,input,inputView,small,smallView,shader,sampler;
 readonly IntPtr[] staging=new IntPtr[3];readonly IntPtr[] inputBinding=new IntPtr[1],outputBinding=new IntPtr[1],samplerBinding=new IntPtr[1],empty=new IntPtr[1];
 int head,count;bool held,hasImage;
 readonly ReadbackWatchdog readbackWatchdog=new ReadbackWatchdog();
 Dx.Acquire acquire;Dx.Result release;Dx.Copy copy;Dx.Map map;Dx.Unmap unmap;Dx.SetResources resources,samplers;Dx.SetUav uavs;Dx.SetShader setShader;Dx.Dispatch dispatch;Dx.Void flush;
 public int Readbacks{get;private set;}public int BusySkips{get;private set;}
 public void Pause(){readbackWatchdog.Pause();}
 public DesktopCapture(string monitor){try{Initialize(monitor);}catch{Dispose();throw;}}
 DesktopCapture(){try{uint level;Dx.Check(Dx.D3D11CreateDevice(IntPtr.Zero,5,IntPtr.Zero,0x20,IntPtr.Zero,0,7,out device,out level,out context),"create the test device");BindMethods();InitializeBuffers();}catch{Dispose();throw;}}
 void Initialize(string monitor){
  IntPtr factory=IntPtr.Zero,adapter=IntPtr.Zero,output=IntPtr.Zero,output1=IntPtr.Zero;
  try{
   var iid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");Dx.Check(Dx.CreateDXGIFactory1(ref iid,out factory),"find the display adapter");
   bool found=false;
   for(uint a=0;!found;a++){
    int hr=Dx.Method<Dx.EnumObject>(factory,12)(factory,a,out adapter);if(hr==unchecked((int)0x887A0002))break;Dx.Check(hr,"enumerate display adapters");
    for(uint o=0;;o++){
     hr=Dx.Method<Dx.EnumObject>(adapter,7)(adapter,o,out output);if(hr==unchecked((int)0x887A0002))break;Dx.Check(hr,"enumerate displays");
     Dx.OutputDesc desc;Dx.Check(Dx.Method<Dx.OutputDescription>(output,7)(output,out desc),"read the display");
     if(desc.Attached!=0&&string.Equals(desc.Name,monitor,StringComparison.OrdinalIgnoreCase)){
      if(desc.Rotation>1)throw new NotSupportedException("For a rotated display, enable Compatibility capture in Strip setup.");
      found=true;break;
     }
     Dx.Free(ref output);
    }
    if(!found)Dx.Free(ref adapter);
   }
   if(!found)throw new IOException("The selected display is no longer connected. Select your screen again.");
   uint level;Dx.Check(Dx.D3D11CreateDevice(adapter,0,IntPtr.Zero,0x20,IntPtr.Zero,0,7,out device,out level,out context),"create the capture device");
   if(level<0xB000)throw new NotSupportedException("This graphics adapter needs Compatibility capture. Enable it in Strip setup.");
   output1=Dx.Query(output,"00cddea8-939b-4b83-a340-a685226666cc");Dx.Check(Dx.Method<Dx.Duplicate>(output1,22)(output1,device,out duplication),"start desktop capture");
   // Lower only this device's GPU scheduling priority; do not compete with presentation.
   IntPtr dxgi=IntPtr.Zero;try{dxgi=Dx.Query(device,"54ec77fa-1377-44e6-8c32-88fd5f44c84c");Dx.Method<Dx.Priority>(dxgi,10)(dxgi,-3);}finally{Dx.Free(ref dxgi);}
   acquire=Dx.Method<Dx.Acquire>(duplication,8);release=Dx.Method<Dx.Result>(duplication,14);BindMethods();InitializeBuffers();
  }finally{Dx.Free(ref output1);Dx.Free(ref output);Dx.Free(ref adapter);Dx.Free(ref factory);}
 }
 void BindMethods(){
   copy=Dx.Method<Dx.Copy>(context,47);map=Dx.Method<Dx.Map>(context,14);unmap=Dx.Method<Dx.Unmap>(context,15);
   resources=Dx.Method<Dx.SetResources>(context,67);uavs=Dx.Method<Dx.SetUav>(context,68);setShader=Dx.Method<Dx.SetShader>(context,69);samplers=Dx.Method<Dx.SetResources>(context,70);dispatch=Dx.Method<Dx.Dispatch>(context,41);flush=Dx.Method<Dx.Void>(context,111);
 }
 void InitializeBuffers(){
   var smallDesc=new Dx.TextureDesc{Width=Capture.W,Height=Capture.H,Mips=1,Array=1,Format=28,SampleCount=1,Bind=128};
   Dx.Check(Dx.Method<Dx.CreateTexture>(device,5)(device,ref smallDesc,IntPtr.Zero,out small),"create the small capture surface");
   Dx.Check(Dx.Method<Dx.CreateView>(device,8)(device,small,IntPtr.Zero,out smallView),"create the colour output");
   smallDesc.Bind=0;smallDesc.Usage=3;smallDesc.Cpu=0x20000;
   for(int i=0;i<staging.Length;i++)Dx.Check(Dx.Method<Dx.CreateTexture>(device,5)(device,ref smallDesc,IntPtr.Zero,out staging[i]),"create a readback buffer");
   var samplerDesc=new Dx.SamplerDesc{Filter=0x15,U=3,V=3,W=3,Anisotropy=1,Comparison=1,MaxLod=float.MaxValue};
   Dx.Check(Dx.Method<Dx.CreateSampler>(device,23)(device,ref samplerDesc,out sampler),"create the colour sampler");
   BuildShader();outputBinding[0]=smallView;samplerBinding[0]=sampler;
 }
 void BuildShader(){
  const string source=@"Texture2D<float4> image : register(t0);
RWTexture2D<float4> result : register(u0);
SamplerState linearClamp : register(s0);
[numthreads(8,8,1)] void main(uint3 id : SV_DispatchThreadID) {
 float2 uv=(float2(id.xy)+0.5)/float2(256,128);
 float2 d=0.25/float2(256,128);
 float3 c=(image.SampleLevel(linearClamp,uv+float2(-d.x,-d.y),0).rgb+
           image.SampleLevel(linearClamp,uv+float2(d.x,-d.y),0).rgb+
           image.SampleLevel(linearClamp,uv+float2(-d.x,d.y),0).rgb+
           image.SampleLevel(linearClamp,uv+d,0).rgb)*0.25;
 result[id.xy]=float4(c.b,c.g,c.r,1);
}";
  IntPtr code=IntPtr.Zero,errors=IntPtr.Zero;
  try{var bytes=Encoding.ASCII.GetBytes(source);int hr=Dx.D3DCompile(bytes,(UIntPtr)bytes.Length,"AmbientLite capture",IntPtr.Zero,IntPtr.Zero,"main","cs_5_0",0x8000,0,out code,out errors);
   if(hr<0){string detail=errors==IntPtr.Zero?"":Marshal.PtrToStringAnsi(Dx.Method<Dx.BlobPointer>(errors,3)(errors));throw new IOException("Could not prepare smooth capture. "+detail);}
   Dx.Check(Dx.Method<Dx.CreateShader>(device,18)(device,Dx.Method<Dx.BlobPointer>(code,3)(code),Dx.Method<Dx.BlobSize>(code,4)(code),IntPtr.Zero,out shader),"create the resize shader");
  }finally{Dx.Free(ref errors);Dx.Free(ref code);}
 }
 void PrepareInput(IntPtr texture){
  Dx.TextureDesc desc;Dx.Method<Dx.TextureDescription>(texture,10)(texture,out desc);
  desc.Mips=1;desc.Array=1;desc.Usage=0;desc.Bind=8;desc.Cpu=0;desc.Misc=0;
  Dx.Check(Dx.Method<Dx.CreateTexture>(device,5)(device,ref desc,IntPtr.Zero,out input),"create the GPU input surface");
  Dx.Check(Dx.Method<Dx.CreateView>(device,7)(device,input,IntPtr.Zero,out inputView),"create the GPU input view");inputBinding[0]=inputView;
 }
 void ReadReady(byte[] pixels){
  // Read older copies only. DO_NOT_WAIT prevents CPU/GPU synchronization stalls.
  while(count>0){Dx.Mapped data;int hr=map(context,staging[head],0,1,0x100000,out data);if(hr==Busy){BusySkips++;if(readbackWatchdog.Busy())throw new COMException("GPU readback stopped making progress.",unchecked((int)0x887A0007));break;}Dx.Check(hr,"read captured colours");readbackWatchdog.Ready();
   try{if(data.RowPitch<Capture.W*4)throw new IOException("Unexpected capture row size.");for(int y=0;y<Capture.H;y++)Marshal.Copy(IntPtr.Add(data.Data,checked(y*(int)data.RowPitch)),pixels,y*Capture.W*4,Capture.W*4);}
   finally{unmap(context,staging[head],0);}head=(head+1)%staging.Length;count--;hasImage=true;Readbacks++;
  }
 }
 void Submit(IntPtr texture){
   if(input==IntPtr.Zero)PrepareInput(texture);
   copy(context,input,texture);setShader(context,shader,IntPtr.Zero,0);resources(context,0,1,inputBinding);uavs(context,0,1,outputBinding,IntPtr.Zero);samplers(context,0,1,samplerBinding);
   dispatch(context,Capture.W/8,Capture.H/8,1);resources(context,0,1,empty);uavs(context,0,1,empty,IntPtr.Zero);
   copy(context,staging[(head+count)%staging.Length],small);count++;flush(context);
 }
 public bool Grab(byte[] pixels){
  ReadReady(pixels);if(count==staging.Length)return hasImage;
  // Hold the previous duplication frame between samples, as recommended by DXGI,
  // so DWM can coalesce updates instead of copying on every monitor refresh.
  if(held){held=false;Dx.Check(release(duplication),"release the previous desktop frame");}
  Dx.FrameInfo info;IntPtr resource;int acquired=acquire(duplication,0,out info,out resource);
  if(acquired==NoFrame)return hasImage;Dx.Check(acquired,"read the desktop frame");held=true;
  IntPtr texture=IntPtr.Zero;
  try{
   if(info.Present==0)return hasImage; // Cursor-only events cannot initialize a desktop image.
   texture=Dx.Query(resource,"6f15aaf2-d208-4e89-9ab4-489535d34f9c");Submit(texture);
  }finally{Dx.Free(ref texture);Dx.Free(ref resource);}
  return hasImage;
 }
 public static void SelfTest(){
  if(Marshal.SizeOf(typeof(Dx.OutputDesc))!=96||Marshal.SizeOf(typeof(Dx.FrameInfo))!=48||Marshal.SizeOf(typeof(Dx.TextureDesc))!=44||Marshal.SizeOf(typeof(Dx.Mapped))!=16||Marshal.SizeOf(typeof(Dx.SamplerDesc))!=52)throw new Exception("Graphics ABI layout mismatch.");
  using(var capture=new DesktopCapture()){
   const int width=512,height=256;var source=new byte[width*height*4];
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){int p=(y*width+x)*4;bool right=x>=width/2,bottom=y>=height/2;source[p]=(byte)(bottom?255:0);source[p+1]=(byte)(right?255:0);source[p+2]=(byte)(right==bottom?255:0);source[p+3]=255;}
   var pinned=GCHandle.Alloc(source,GCHandleType.Pinned);IntPtr data=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Dx.SubresourceData))),texture=IntPtr.Zero;
   try{
    var initial=new Dx.SubresourceData{Data=pinned.AddrOfPinnedObject(),RowPitch=width*4};Marshal.StructureToPtr(initial,data,false);
    var desc=new Dx.TextureDesc{Width=width,Height=height,Mips=1,Array=1,Format=87,SampleCount=1,Bind=8};
    Dx.Check(Dx.Method<Dx.CreateTexture>(capture.device,5)(capture.device,ref desc,data,out texture),"create the test pattern");
    var pixels=new byte[Capture.W*Capture.H*4];capture.ReadReady(pixels);if(capture.hasImage)throw new Exception("Capture returned an image before readback.");capture.Submit(texture);
    var timeout=Stopwatch.StartNew();while(!capture.hasImage&&timeout.Elapsed.TotalSeconds<5){capture.ReadReady(pixels);if(!capture.hasImage)System.Threading.Thread.Sleep(1);}
    if(!capture.hasImage)throw new Exception("Capture readback timed out.");
    int[] xs={64,192,64,192},ys={32,32,96,96};int[,] expected={{0,0,255},{0,255,0},{255,0,0},{255,255,255}};
    for(int i=0;i<4;i++)for(int c=0;c<3;c++)if(Math.Abs(pixels[(ys[i]*Capture.W+xs[i])*4+c]-expected[i,c])>1)throw new Exception("GPU downsampling, channel order, or row layout failed.");
   }finally{Dx.Free(ref texture);Marshal.FreeHGlobal(data);pinned.Free();}
  }
  Console.WriteLine("Graphics ABI, GPU resize, channel order, row layout, and readback checks passed (WARP).");
 }
 public void Dispose(){
  if(held&&duplication!=IntPtr.Zero){held=false;if(release!=null)release(duplication);}
  if(context!=IntPtr.Zero)Dx.Method<Dx.Void>(context,110)(context);
  Dx.Free(ref inputView);Dx.Free(ref input);Dx.Free(ref smallView);Dx.Free(ref small);Dx.Free(ref sampler);Dx.Free(ref shader);
  for(int i=0;i<staging.Length;i++)Dx.Free(ref staging[i]);Dx.Free(ref duplication);Dx.Free(ref context);Dx.Free(ref device);
 }
}

sealed class LegacyCapture : IDisposable {
 IntPtr dc,bmp,old,bits;
 public LegacyCapture(){IntPtr screen=Native.GetDC(IntPtr.Zero);try{dc=Native.CreateCompatibleDC(screen);var info=new Native.BitmapInfo{size=40,width=Capture.W,height=-Capture.H,planes=1,bits=32};bmp=Native.CreateDIBSection(screen,ref info,0,out bits,IntPtr.Zero,0);if(screen==IntPtr.Zero||dc==IntPtr.Zero||bmp==IntPtr.Zero)throw new IOException("Cannot create compatibility capture surface");old=Native.SelectObject(dc,bmp);Native.SetStretchBltMode(dc,3);}catch{Dispose();throw;}finally{if(screen!=IntPtr.Zero)Native.ReleaseDC(IntPtr.Zero,screen);}}
 public bool Grab(Rectangle bounds,byte[] pixels){IntPtr screen=Native.GetDC(IntPtr.Zero);try{if(!Native.StretchBlt(dc,0,0,Capture.W,Capture.H,screen,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x40CC0020))throw new Win32Exception("Screen capture failed; try the normal desktop");Marshal.Copy(bits,pixels,0,pixels.Length);return true;}finally{if(screen!=IntPtr.Zero)Native.ReleaseDC(IntPtr.Zero,screen);}}
 public void Dispose(){if(old!=IntPtr.Zero&&dc!=IntPtr.Zero)Native.SelectObject(dc,old);if(bmp!=IntPtr.Zero)Native.DeleteObject(bmp);if(dc!=IntPtr.Zero)Native.DeleteDC(dc);bmp=dc=old=bits=IntPtr.Zero;}
}
