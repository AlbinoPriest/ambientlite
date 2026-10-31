using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

// Explicit, bounded troubleshooting only. No pixels, network, or extra capture.
static class ColourTrace {
 static readonly string path=FindPath();static long remaining=1048576;
 public static bool Enabled {get{return path!=null&&remaining>0;}}
 static string FindPath(){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--colour-log");return at>=0&&at+1<args.Length?args[at+1]:Environment.GetEnvironmentVariable("AMBIENTLITE_COLOUR_LOG");}
 internal static string Colours(Color[] colors){return colors==null?"none":string.Join(",",colors.Select(c=>c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2")));}
 internal static string PacketColours(byte[] packet){if(packet==null||packet.Length<12||(packet.Length-7)%5!=0)throw new ArgumentException("Invalid LED frame");int count=(packet.Length-7)/5;var colors=new Color[count];for(int i=0;i<count;i++){int p=6+i*5;colors[i]=Color.FromArgb(packet[p+1],packet[p+2],packet[p+3]);}return Colours(colors);}
 public static void Write(string monitor,int mode,int brightness,int smoothing,int readbacks,int busySkips,Color[] sampled,byte[] packet,bool sent){
  if(!Enabled)return;
  try{string line=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture)+" monitor="+monitor+" mode="+mode+" brightness="+brightness+" smoothing="+smoothing+" readbacks="+readbacks+" busy="+busySkips+" sent="+sent+" sequence="+packet[4]+" raw="+Colours(sampled)+" packet="+PacketColours(packet)+Environment.NewLine;
   remaining-=System.Text.Encoding.UTF8.GetByteCount(line);if(remaining<0)return;File.AppendAllText(path,line);
  }catch{remaining=0;} // Diagnostics must never interrupt the lighting worker.
 }
}
