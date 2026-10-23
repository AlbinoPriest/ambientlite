using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

sealed partial class LightWindow {
 Window window; ComboBox monitor; Slider brightness,smoothing,rate; TextBox topCount,sideCount; CheckBox reverse;
 Button sync,test; Forms.NotifyIcon tray; CancellationTokenSource cancellation; Task worker;
 Forms.Screen[] screens; bool exiting,testing,loading; string page="Lighting"; int frameCount;
 volatile int liveBrightness=70,liveSmoothing=45,liveRate=20,liveMode=0,liveColour=0xFFC885;
 ColourWheel wheel; string colourName="Candle"; SemaphoreSlim changes=new SemaphoreSlim(0,1);
 string[] colourNames={"Candle","Soft white","Coral","Rose","Lavender","Blue hour","Sea glass","Daylight"};
 string[] colourHex={"#FFC885","#FFF1DA","#FF9A88","#FFA3C7","#C7B0FF","#8CBFFF","#85FFD8","#E5F2FF"};
 System.Collections.Generic.List<Button> swatches=new System.Collections.Generic.List<Button>();
 string settings=AppEnvironment.Settings; DispatcherTimer statsTimer;
 static Brush Brush(string hex){return (Brush)new BrushConverter().ConvertFromString(hex);}
 T Get<T>(string name) where T:class {return window.FindName(name) as T;}
 void Text(string name,string value){Get<TextBlock>(name).Text=value;}
 public Window Window{get{return window;}}
 public LightWindow(){
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Ambient.UI"))window=(Window)XamlReader.Load(stream);
  using(var icon=Assembly.GetExecutingAssembly().GetManifestResourceStream("Ambient.Icon")){var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=icon;bitmap.EndInit();window.Icon=bitmap;}
  monitor=Get<ComboBox>("Monitor");brightness=Get<Slider>("Brightness");smoothing=Get<Slider>("Smoothing");rate=Get<Slider>("Rate");topCount=Get<TextBox>("TopCount");sideCount=Get<TextBox>("SideCount");reverse=Get<CheckBox>("Reverse");sync=Get<Button>("SyncButton");test=Get<Button>("TestButton");
  screens=Forms.Screen.AllScreens;for(int i=0;i<screens.Length;i++){var s=screens[i];monitor.Items.Add("Screen "+(i+1)+"  ·  "+s.Bounds.Width+" × "+s.Bounds.Height+(s.Primary?"  ·  Main display":""));}monitor.SelectedIndex=Math.Max(0,Array.FindIndex(screens,s=>s.DeviceName.EndsWith("DISPLAY2")));
  loading=true;if(!renderingDocumentation)Load();SetupColours();loading=false;UpdateValues();
  brightness.ValueChanged+=(s,e)=>UpdateValues();smoothing.ValueChanged+=(s,e)=>UpdateValues();rate.ValueChanged+=(s,e)=>UpdateValues();topCount.TextChanged+=(s,e)=>UpdateLayout();sideCount.TextChanged+=(s,e)=>UpdateLayout();reverse.Checked+=(s,e)=>SaveIfValid();reverse.Unchecked+=(s,e)=>SaveIfValid();monitor.SelectionChanged+=(s,e)=>SaveIfValid();
  Get<Button>("LightingNav").Click+=(s,e)=>Navigate("Lighting");Get<Button>("ColourNav").Click+=(s,e)=>Navigate("Colour");Get<Button>("SetupNav").Click+=(s,e)=>Navigate("Setup");Get<Button>("StartupNav").Click+=(s,e)=>Navigate("Startup");SetupStartup();
  sync.Click+=async(s,e)=>{if(IsRunning)await Stop();else await Start(false);};test.Click+=async(s,e)=>await Start(true);
  tray=new Forms.NotifyIcon{Text="AmbientLite · Ready",Visible=!renderingDocumentation};SetIcon(false);SetupQuickControls();
  window.StateChanged+=(s,e)=>{if(window.WindowState==WindowState.Minimized)window.Hide();};
  window.Closing+=(s,e)=>{if(!exiting){e.Cancel=true;SaveIfValid();window.Hide();}else{SaveIfValid();statsTimer.Stop();tray.Visible=false;var icon=tray.Icon;tray.Dispose();if(icon!=null)icon.Dispose();}};
  window.IsVisibleChanged+=(s,e)=>{Interlocked.Exchange(ref frameCount,0);if(window.IsVisible)statsTimer.Start();else statsTimer.Stop();};
  statsTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};statsTimer.Tick+=(s,e)=>{int count=Interlocked.Exchange(ref frameCount,0);Text("RunStats",IsRunning&&!testing?(liveMode==1?"Static colour  ·  USB connected":count+" fps  ·  USB connected"):"Local. Quiet. Yours.");};statsTimer.Start();
  UpdateLayout();if(liveMode==1)Navigate("Colour");
 }
 bool IsRunning{get{return worker!=null&&!worker.IsCompleted;}}
 void Show(){HideQuick();window.ShowInTaskbar=true;window.Show();window.WindowState=WindowState.Normal;window.Activate();}
 void Navigate(string target){page=target;if(target=="Lighting")liveMode=0;else if(target=="Colour")liveMode=1;foreach(string n in new[]{"Lighting","Colour","Setup","Startup"}){Get<Grid>(n+"Page").Visibility=target==n?Visibility.Visible:Visibility.Collapsed;var button=Get<Button>(n+"Nav");button.Background=Brush(n==target?"#292D33":"#00000000");button.Foreground=Brush(n==target?"#F1EDE8":"#B2B0B0");}Text("PageTitle",target=="Lighting"?"A little beyond the screen.":target=="Colour"?"Find your kind of glow.":target=="Startup"?"A warmer welcome home.":"Made for your space.");Text("PageSubtitle",target=="Lighting"?"Colour that follows the moment.":target=="Colour"?"One colour. For whatever the evening brings.":target=="Startup"?"Your glow, ready when you are.":"A few details. A better fit.");if(IsRunning&&!testing)ActiveState();else if(!IsRunning)Text("StateDetail",liveMode==1?"Choose a colour, then start lighting.":"Choose your screen and let the colour flow.");Wake();SaveIfValid();UpdateQuick();}
 void Wake(){try{changes.Release();}catch(SemaphoreFullException){}}
 void ActiveState(){State(true,true,liveMode==1?"A steady glow.":"Lighting your space",liveMode==1?"Your chosen colour, across the whole strip.":"Following your screen. Adjust the feeling as you go.");}
 void SetupColours(){wheel=new ColourWheel{Width=270,Height=270};Get<Grid>("WheelHost").Children.Add(wheel);wheel.Changed+=color=>ChooseColour(color,"Custom colour");var panel=Get<System.Windows.Controls.Primitives.UniformGrid>("Swatches");for(int i=0;i<colourHex.Length;i++){int index=i;var color=(Color)ColorConverter.ConvertFromString(colourHex[i]);var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new System.Windows.Shapes.Ellipse{Width=17,Height=17,Fill=new SolidColorBrush(color),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center});row.Children.Add(new TextBlock{Text=colourNames[i],FontSize=11,VerticalAlignment=VerticalAlignment.Center});var button=new Button{Content=row,Margin=new Thickness(3),Padding=new Thickness(9,10,9,10),HorizontalContentAlignment=HorizontalAlignment.Left,ToolTip=colourNames[i]+" · "+colourHex[i]};System.Windows.Automation.AutomationProperties.SetName(button,colourNames[i]);button.Click+=(s,e)=>ChooseColour((Color)ColorConverter.ConvertFromString(colourHex[index]),colourNames[index]);swatches.Add(button);panel.Children.Add(button);}ChooseColour(Color.FromRgb((byte)(liveColour>>16),(byte)(liveColour>>8),(byte)liveColour),null);}
 void ChooseColour(Color color,string name){liveColour=(color.R<<16)|(color.G<<8)|color.B;string hex="#"+liveColour.ToString("X6");int known=Array.IndexOf(colourHex,hex);colourName=known>=0?colourNames[known]:name??"Custom colour";Text("ChosenName",colourName);Text("ChosenHex",hex);Get<System.Windows.Shapes.Ellipse>("ChosenDot").Fill=new SolidColorBrush(color);if(wheel!=null)wheel.Select(color);for(int i=0;i<swatches.Count;i++){swatches[i].BorderBrush=Brush(i==known?"#F39485":"#363A41");swatches[i].Background=Brush(i==known?"#393035":"#292D33");}Wake();SaveIfValid();UpdateQuick();}
 void UpdateValues(){liveBrightness=(int)Math.Round(brightness.Value);liveSmoothing=(int)Math.Round(smoothing.Value);liveRate=(int)Math.Round(rate.Value);Text("BrightnessValue",liveBrightness+"%");Text("SmoothingValue",liveSmoothing+"%");Text("RateValue",liveRate+" fps");Wake();SaveIfValid();UpdateQuick();}
 bool Layout(out int top,out int side){top=side=0;return int.TryParse(topCount.Text,out top)&&int.TryParse(sideCount.Text,out side)&&top>0&&side>0&&top<=254&&side<=126&&top+2*side<=254;}
 void UpdateLayout(){int t,s;bool valid=Layout(out t,out s);Text("LayoutSummary",valid?(t+2*s)+" lights · three sides":"Check strip setup");Text("SetupHelp",valid?"Pause lighting before changing the strip layout.":"Use positive whole numbers, with at most 254 lights in total.");if(!IsRunning)sync.IsEnabled=test.IsEnabled=valid;SaveIfValid();}
 void SaveIfValid(){if(renderingDocumentation||loading||monitor.SelectedIndex<0)return;int t,s;if(!Layout(out t,out s))return;try{AppEnvironment.Save(new[]{screens[monitor.SelectedIndex].DeviceName,liveRate.ToString(),liveBrightness.ToString(),liveSmoothing.ToString(),t.ToString(),s.ToString(),(reverse.IsChecked==true).ToString(),liveMode.ToString(),"#"+liveColour.ToString("X6"),startInTray.ToString(),startLighting.ToString()});}catch(Exception ex){Text("RunStats","Settings could not be saved");Debug.WriteLine(ex.Message);}}
 void Load(){try{var a=File.ReadAllLines(settings);int index=Array.FindIndex(screens,s=>s.DeviceName==a[0]);if(index>=0)monitor.SelectedIndex=index;int v;if(int.TryParse(a[1],out v))rate.Value=Math.Max(5,Math.Min(60,v));if(int.TryParse(a[2],out v))brightness.Value=Math.Max(0,Math.Min(100,v));if(int.TryParse(a[3],out v))smoothing.Value=Math.Max(0,Math.Min(95,v));topCount.Text=a[4];sideCount.Text=a[5];reverse.IsChecked=bool.Parse(a[6]);if(a.Length>7)liveMode=a[7]=="1"?1:0;if(a.Length>8){var selected=(Color)ColorConverter.ConvertFromString(a[8]);liveColour=(selected.R<<16)|(selected.G<<8)|selected.B;}bool flag;if(a.Length>9&&bool.TryParse(a[9],out flag))startInTray=flag;if(a.Length>10&&bool.TryParse(a[10],out flag))startLighting=flag;}catch{}}
 void State(bool running,bool connected,string title,string detail){sync.Content=running?"Pause lighting":"Start lighting";Get<System.Windows.Shapes.Ellipse>("StateDot").Fill=Brush(connected?"#F39485":"#737780");Text("StateTitle",title);Text("StateDetail",detail);monitor.IsEnabled=topCount.IsEnabled=sideCount.IsEnabled=reverse.IsEnabled=!running;test.IsEnabled=!running;tray.Text=connected?"AmbientLite · Lighting active":"AmbientLite · Paused";SetIcon(connected);UpdateQuick();}
 void SetIcon(bool active){using(var bmp=new Drawing.Bitmap(32,32))using(var g=Drawing.Graphics.FromImage(bmp)){g.SmoothingMode=Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(Drawing.Color.Transparent);using(var p=new Drawing.Pen(active?Drawing.Color.FromArgb(243,148,133):Drawing.Color.FromArgb(178,176,176),3)){g.DrawLines(p,new[]{new Drawing.Point(3,24),new Drawing.Point(3,6),new Drawing.Point(29,6),new Drawing.Point(29,24)});g.DrawRectangle(p,8,11,16,12);g.DrawLine(p,12,28,20,28);}IntPtr h=bmp.GetHicon();try{var old=tray.Icon;using(var temp=Drawing.Icon.FromHandle(h))tray.Icon=(Drawing.Icon)temp.Clone();if(old!=null)old.Dispose();}finally{DestroyIcon(h);}}}
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
 async Task Start(bool solid){if(cancellation!=null)return;if(Process.GetProcessesByName("SyncLight").Length>0){State(false,false,"SyncLight is still open","Exit SyncLight from its tray icon, then start lighting here.");return;}int top,side;if(!Layout(out top,out side)){Navigate("Setup");return;}var bounds=screens[monitor.SelectedIndex].Bounds;bool rev=reverse.IsChecked==true;testing=solid;var tokenSource=new CancellationTokenSource();cancellation=tokenSource;var token=tokenSource.Token;State(true,false,"Connecting to your lights…","Getting everything ready.");sync.IsEnabled=false;UpdateQuick();
  worker=Task.Run(async()=>{var devices=Hid.Find();if(devices.Length!=1)throw new IOException(devices.Length==0?"Plug in your light strip and try again.":"More than one light controller is connected.");using(var hid=new Hid(devices[0])){Capture capture=null;try{
   await window.Dispatcher.InvokeAsync(()=>{if(solid)State(true,true,"A little blue for you.","Checking every light on the strip.");else ActiveState();sync.IsEnabled=true;UpdateQuick();});
   var last=new Drawing.Color[top+side*2];byte[] previous=null;int seq=1;var sent=Stopwatch.StartNew();
   while(!token.IsCancellationRequested){var tick=Stopwatch.StartNew();Drawing.Color[] colors;if(solid)colors=Enumerable.Repeat(Drawing.Color.FromArgb(0,0,40),last.Length).ToArray();else if(liveMode==1){if(capture!=null){capture.Dispose();capture=null;}int rgb=liveColour;double power=liveBrightness/100.0;var chosen=Drawing.Color.FromArgb((int)(((rgb>>16)&255)*power),(int)(((rgb>>8)&255)*power),(int)((rgb&255)*power));colors=Enumerable.Repeat(chosen,last.Length).ToArray();}else{if(capture==null)capture=new Capture();capture.Grab(bounds);colors=capture.Edge(side,top,rev);double power=liveBrightness/100.0,blend=1-liveSmoothing/100.0;for(int i=0;i<colors.Length;i++){var c=colors[i];colors[i]=Drawing.Color.FromArgb((int)(last[i].R*(1-blend)+c.R*power*blend),(int)(last[i].G*(1-blend)+c.G*power*blend),(int)(last[i].B*(1-blend)+c.B*power*blend));}last=colors;}
    var packet=Protocol.Frame(colors,seq);bool changed=previous==null;for(int i=6;!changed&&i<packet.Length-1;i++)if(packet[i]!=previous[i])changed=true;if(changed||sent.ElapsedMilliseconds>=1000){await hid.Send(packet,token);previous=packet;sent.Restart();seq=seq%254+1;}if(liveMode==0)Interlocked.Increment(ref frameCount);if(solid)break;if(liveMode==1)await changes.WaitAsync(token);else await Task.Delay(Math.Max(1,1000/liveRate-(int)tick.ElapsedMilliseconds),token);
   }
  }finally{if(capture!=null)capture.Dispose();}}});
  string error=null;try{await worker;}catch(OperationCanceledException){}catch(Exception ex){error=ex.Message;}
  finally{if(cancellation==tokenSource)cancellation=null;tokenSource.Dispose();testing=false;Interlocked.Exchange(ref frameCount,0);sync.IsEnabled=true;if(error==null)State(false,false,solid?"Your lights are connected.":"Taking a little pause.",solid?"Blue sent to the whole strip. Ready to follow your screen.":"Your lights keep their last colour. Start again whenever you like.");else State(false,false,"Your lights need a moment.",error);UpdateLayout();UpdateQuick();}
 }
 async Task Stop(){var source=cancellation;var running=worker;if(source==null)return;sync.IsEnabled=false;UpdateQuick();source.Cancel();try{await running;}catch{}// Start owns disposal and restores the interface.
 }
}
