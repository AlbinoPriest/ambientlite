using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

sealed partial class LightWindow {
 Window flyout; DispatcherTimer trayClickDelay; bool refreshingQuick;
 System.Drawing.Point quickAnchor; DateTime ignoreQuickClicksUntil;
 readonly System.Collections.Generic.List<Button> quickSwatches=new System.Collections.Generic.List<Button>();
 [System.Runtime.InteropServices.DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 void ApplyWindowColours(){try{IntPtr h=new WindowInteropHelper(window).Handle;int dark=1,caption=0x001C1917,text=0x00E8EDF1,border=0x00413A36,round=2;if(DwmSetWindowAttribute(h,20,ref dark,4)!=0)DwmSetWindowAttribute(h,19,ref dark,4);DwmSetWindowAttribute(h,35,ref caption,4);DwmSetWindowAttribute(h,36,ref text,4);DwmSetWindowAttribute(h,34,ref border,4);DwmSetWindowAttribute(h,33,ref round,4);}catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex.Message);}}
 void SetupQuickControls(){
  window.SourceInitialized+=(s,e)=>ApplyWindowColours();
  Get<Button>("QuickControlsButton").Click+=(s,e)=>ToggleQuick();
  trayClickDelay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime)};
  trayClickDelay.Tick+=(s,e)=>{trayClickDelay.Stop();ToggleQuick();};
  tray.MouseClick+=(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>{if(DateTime.UtcNow<ignoreQuickClicksUntil)return;if(e.Button==Forms.MouseButtons.Left){trayClickDelay.Stop();trayClickDelay.Start();}else if(e.Button==Forms.MouseButtons.Right){trayClickDelay.Stop();ToggleQuick();}}));
  tray.MouseDoubleClick+=(s,e)=>{if(e.Button==Forms.MouseButtons.Left)window.Dispatcher.BeginInvoke(new Action(()=>{ignoreQuickClicksUntil=DateTime.UtcNow.AddMilliseconds(Forms.SystemInformation.DoubleClickTime);trayClickDelay.Stop();HideQuick();Show();}));};
  window.Closed+=(s,e)=>{trayClickDelay.Stop();if(flyout!=null)flyout.Close();};
  if(Environment.GetCommandLineArgs().Contains("--quick-controls"))window.Loaded+=(s,e)=>OpenQuick();
 }
 T Quick<T>(string name)where T:class{return flyout.FindName(name)as T;}
 void HideQuick(){if(flyout!=null)flyout.Hide();}
 void ToggleQuick(){if(flyout!=null&&flyout.IsVisible)HideQuick();else OpenQuick();}
 void OpenQuick(){
  if(flyout==null){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Ambient.Tray"))flyout=(Window)XamlReader.Load(stream);
   flyout.Resources.MergedDictionaries.Add(window.Resources);
   if(Environment.GetCommandLineArgs().Contains("--inspect-quick"))flyout.ShowInTaskbar=true;
   flyout.Deactivated+=(s,e)=>{if(!Environment.GetCommandLineArgs().Contains("--inspect-quick"))HideQuick();};
   flyout.SizeChanged+=(s,e)=>{if(flyout.IsVisible)PlaceQuick();};
   flyout.PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){HideQuick();e.Handled=true;}};
   Quick<Button>("QuickClose").Click+=(s,e)=>HideQuick();
   Quick<Button>("QuickOpen").Click+=(s,e)=>Show();
   Quick<Button>("QuickExit").Click+=async(s,e)=>await ExitApplication();
   Quick<Button>("QuickToggle").Click+=async(s,e)=>{Quick<Button>("QuickToggle").IsEnabled=false;if(IsRunning)await Stop();else await Start(false);UpdateQuick();};
   Quick<RadioButton>("QuickScreen").Checked+=(s,e)=>{if(!refreshingQuick)Navigate("Lighting");};
   Quick<RadioButton>("QuickStatic").Checked+=(s,e)=>{if(!refreshingQuick)Navigate("Colour");};
   BindQuick("QuickBrightness",brightness);BindQuick("QuickSmoothing",smoothing);BindQuick("QuickRate",rate);
   for(int i=0;i<colourNames.Length;i++){int index=i;var c=(Color)ColorConverter.ConvertFromString(colourHex[i]);var stack=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};stack.Children.Add(new System.Windows.Shapes.Ellipse{Width=15,Height=15,Fill=new SolidColorBrush(c),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,6)});stack.Children.Add(new TextBlock{Text=colourNames[i],FontSize=9,HorizontalAlignment=HorizontalAlignment.Center});var b=new Button{Content=stack,Margin=new Thickness(3),Padding=new Thickness(3,8,3,8),HorizontalContentAlignment=HorizontalAlignment.Center,ToolTip=colourNames[i]+" · "+colourHex[i]};System.Windows.Automation.AutomationProperties.SetName(b,colourNames[i]);b.Click+=(s,e)=>ChooseColour((Color)ColorConverter.ConvertFromString(colourHex[index]),colourNames[index]);quickSwatches.Add(b);Quick<System.Windows.Controls.Primitives.UniformGrid>("QuickSwatches").Children.Add(b);}
  }
  quickAnchor=Forms.Cursor.Position;UpdateQuick();flyout.Opacity=0;flyout.Show();flyout.UpdateLayout();PlaceQuick();flyout.Opacity=1;flyout.Activate();
 }
 void BindQuick(string name,Slider original){Quick<Slider>(name).SetBinding(Slider.ValueProperty,new Binding("Value"){Source=original,Mode=BindingMode.TwoWay});}
 void PlaceQuick(){var cursor=quickAnchor;var area=Forms.Screen.FromPoint(cursor).WorkingArea;var source=PresentationSource.FromVisual(flyout);if(source==null||source.CompositionTarget==null)return;var transform=source.CompositionTarget.TransformFromDevice;Point p=transform.Transform(new Point(cursor.X,cursor.Y)),a=transform.Transform(new Point(area.Left,area.Top)),b=transform.Transform(new Point(area.Right,area.Bottom));double w=flyout.ActualWidth,h=flyout.ActualHeight;flyout.Left=Math.Max(a.X+8,Math.Min(p.X-w+24,b.X-w-8));double y=p.Y-h-12;if(y<a.Y+8)y=p.Y+12;flyout.Top=Math.Max(a.Y+8,Math.Min(y,b.Y-h-8));}
 void UpdateQuick(){if(flyout==null||refreshingQuick)return;refreshingQuick=true;try{
  Quick<TextBlock>("QuickTitle").Text=Get<TextBlock>("StateTitle").Text;
  Quick<TextBlock>("QuickDetail").Text=Get<TextBlock>("StateDetail").Text;
  Quick<System.Windows.Shapes.Ellipse>("QuickDot").Fill=Get<System.Windows.Shapes.Ellipse>("StateDot").Fill;
  Quick<Button>("QuickToggle").Content=sync.Content;Quick<Button>("QuickToggle").IsEnabled=sync.IsEnabled;
  Quick<RadioButton>("QuickScreen").IsChecked=liveMode==0;Quick<RadioButton>("QuickStatic").IsChecked=liveMode==1;
  Quick<TextBlock>("QuickBrightnessText").Text=liveBrightness+"%";Quick<TextBlock>("QuickSmoothingText").Text=liveSmoothing+"%";Quick<TextBlock>("QuickRateText").Text=liveRate+" fps";
  Quick<StackPanel>("QuickColours").Visibility=liveMode==1?Visibility.Visible:Visibility.Collapsed;Quick<StackPanel>("QuickScreenSettings").Visibility=liveMode==0?Visibility.Visible:Visibility.Collapsed;
  Quick<TextBlock>("QuickColourName").Text=colourName+" · #"+liveColour.ToString("X6");Quick<System.Windows.Shapes.Ellipse>("QuickColourDot").Fill=Brush("#"+liveColour.ToString("X6"));
  for(int i=0;i<quickSwatches.Count;i++){bool selected=colourHex[i]=="#"+liveColour.ToString("X6");quickSwatches[i].BorderBrush=Brush(selected?"#F39485":"#363A41");quickSwatches[i].Background=Brush(selected?"#393035":"#292D33");}
 }finally{refreshingQuick=false;}}
}
