using System;
using System.IO;
using AmbientLite.Control;
static class ControlProbe {
 static int Main(string[] args){try{var request=new Request{Command=args.Length==0?"status":args[0]};if(args.Length==2&&args[0]=="restore"){var saved=Wire.Decode<Reply>(File.ReadAllBytes(args[1]));var current=Wire.Call(new Request{Command="status"}).GetAwaiter().GetResult();request=new Request{Command="apply",Settings=saved.Settings,ExpectedKey=current.SettingsKey};}var reply=Wire.Call(request).GetAwaiter().GetResult();Console.WriteLine(System.Text.Encoding.UTF8.GetString(Wire.Encode(reply)));return reply.Ok?0:1;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
