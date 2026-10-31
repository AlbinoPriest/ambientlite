using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using AmbientLite.Control;
static class AmbientControlChecks {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(IOException){return;}throw new Exception("Invalid control input accepted");}
 static Options Valid(){return new Options{Mode="screen",Colour="#FF541F",Monitor="display",Brightness=100,Smoothing=45,Rate=20,Top=41,Side=15};}
 static Reply Call(string name,int version){using(var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous)){pipe.Connect(2000);Wire.Send(pipe,new Request{Version=version,Command="status"},CancellationToken.None).GetAwaiter().GetResult();return Wire.Receive<Reply>(pipe,CancellationToken.None).GetAwaiter().GetResult();}}
 static int Main(){try{
  var legacy=Wire.Decode<Options>(System.Text.Encoding.UTF8.GetBytes("{\"Mode\":\"screen\"}"));Assert(!legacy.Warmth.HasValue&&LightWindow.ResolveWarmth(legacy,63)==63,"Older clients must preserve saved warmth.");
  var neutral=Valid();neutral.Warmth=0;Assert(LightWindow.ResolveWarmth(neutral,63)==0,"Explicit neutral must reset warmth.");
  neutral.Warmth=101;Reject(()=>LightWindow.ValidateControlOptions(neutral));neutral.Warmth=-1;Reject(()=>LightWindow.ValidateControlOptions(neutral));
  var o=Valid();LightWindow.ValidateControlOptions(o);o.Top=Int32.MaxValue;Reject(()=>LightWindow.ValidateControlOptions(o));o=Valid();o.Side=0;Reject(()=>LightWindow.ValidateControlOptions(o));o=Valid();o.Colour="red";Reject(()=>LightWindow.ValidateControlOptions(o));o=Valid();o.Rate=61;Reject(()=>LightWindow.ValidateControlOptions(o));
  using(var stream=new MemoryStream()){Wire.Send(stream,new Request{Command="apply",Settings=Valid()},CancellationToken.None).GetAwaiter().GetResult();stream.Position=0;var r=Wire.Receive<Request>(stream,CancellationToken.None).GetAwaiter().GetResult();Assert(r.Settings.Top==41&&r.Command=="apply"&&r.Version==1,"Framed settings roundtrip");}
  foreach(byte[] bytes in new[]{new byte[0],new byte[]{1,0,0},new byte[]{255,255,255,255},new byte[]{0,0,0,0},new byte[]{5,0,0,0,123}})Reject(()=>Wire.Receive<Request>(new MemoryStream(bytes),CancellationToken.None).GetAwaiter().GetResult());
  string name="AmbientLite.Control.Test."+Guid.NewGuid().ToString("N");int calls=0;using(var server=new LocalControl(r=>{calls++;return Task.FromResult(new Reply{Ok=true,Title="Ready"});},name)){
   var reply=Call(name,1);Assert(reply.Ok&&reply.Title=="Ready","Same-user local pipe roundtrip");
   reply=Call(name,99);Assert(!reply.Ok&&calls==1,"Unknown version never reaches handler");
   using(var broken=new NamedPipeClientStream(".",name,PipeDirection.InOut)){broken.Connect(2000);broken.Write(new byte[]{20,0,0,0},0,4);}
   reply=Call(name,1);Assert(reply.Ok,"Server recovers after a truncated client request");
  }
  Console.WriteLine("Control framing, bounds, local pipe, version rejection and disconnected-client recovery passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
