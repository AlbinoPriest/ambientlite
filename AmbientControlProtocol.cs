// MIT License. Copyright (c) 2026 AlbinoPriest. See LICENSE.
// Local control protocol v1. Canonical source: AmbientLite repository.
using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientLite.Control {
 [DataContract] public sealed class Options {
  [DataMember] public string Mode,Colour,Monitor;
  [DataMember] public int Brightness,Smoothing,Rate,Top,Side;
  [DataMember] public bool Reverse,Compatibility,OffOnExit,StartInTray,StartLighting,StartWithWindows;
 }
 [DataContract] public sealed class Display {
  [DataMember] public string Id,Name;
 }
 [DataContract] public sealed class Request {
  [DataMember] public int Version=1;
  [DataMember] public string Command,ExpectedKey;
  [DataMember] public Options Settings;
 }
 [DataContract] public sealed class Reply {
  [DataMember] public int Version=1;
  [DataMember] public bool Ok,Running,Companion;
  [DataMember] public int ScreenFrames,ProcessId;
  [DataMember] public string Error,AppVersion,Title,Detail,SettingsKey;
  [DataMember] public Options Settings;
  [DataMember] public Display[] Displays;
 }
 public static class Wire {
  public const int Limit=16384;
  public static string PipeName {get{return "AmbientLite.Control.v1."+WindowsIdentity.GetCurrent().User.Value;}}
  public static byte[] Encode<T>(T value) {using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);if(stream.Length>Limit)throw new IOException("Control message is too large.");return stream.ToArray();}}
  public static T Decode<T>(byte[] bytes) {if(bytes==null||bytes.Length==0||bytes.Length>Limit)throw new IOException("Invalid control message length.");using(var stream=new MemoryStream(bytes))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
  public static async Task Send<T>(Stream stream,T value,CancellationToken token) {
   byte[] bytes=Encode(value);int n=bytes.Length;byte[] length={(byte)n,(byte)(n>>8),(byte)(n>>16),(byte)(n>>24)};
   await stream.WriteAsync(length,0,4,token).ConfigureAwait(false);await stream.WriteAsync(bytes,0,n,token).ConfigureAwait(false);await stream.FlushAsync(token).ConfigureAwait(false);
  }
  static async Task<byte[]> Read(Stream stream,int size,CancellationToken token) {byte[] b=new byte[size];int at=0;while(at<size){int n=await stream.ReadAsync(b,at,size-at,token).ConfigureAwait(false);if(n==0)throw new EndOfStreamException("AmbientLite disconnected.");at+=n;}return b;}
  public static async Task<T> Receive<T>(Stream stream,CancellationToken token) {
   byte[] b=await Read(stream,4,token).ConfigureAwait(false);uint size=(uint)(b[0]|b[1]<<8|b[2]<<16|b[3]<<24);
   if(size==0||size>Limit)throw new IOException("Invalid control message length.");return Decode<T>(await Read(stream,(int)size,token).ConfigureAwait(false));
  }
  public static async Task<Reply> Call(Request request,int timeout=5000) {
   using(var budget=new CancellationTokenSource(timeout))
   using(var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous))
   using(budget.Token.Register(()=>pipe.Dispose())) {
    try{await pipe.ConnectAsync(Math.Min(timeout,1000),budget.Token).ConfigureAwait(false);await Send(pipe,request,budget.Token).ConfigureAwait(false);var reply=await Receive<Reply>(pipe,budget.Token).ConfigureAwait(false);if(reply==null||reply.Version!=1)throw new NotSupportedException("AmbientLite control version is incompatible. Update both apps.");return reply;}
    catch(Exception e){if(budget.IsCancellationRequested)throw new TimeoutException("AmbientLite did not respond in time. Refresh before repeating a change.",e);throw;}
   }
  }
 }
}
