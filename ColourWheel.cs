using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

static class ColourMath {
 public static Color FromHsv(double hue,double saturation,double value){hue=(hue%360+360)%360;saturation=Math.Max(0,Math.Min(1,saturation));value=Math.Max(0,Math.Min(1,value));double c=value*saturation,x=c*(1-Math.Abs(hue/60%2-1)),m=value-c;double r=0,g=0,b=0;if(hue<60){r=c;g=x;}else if(hue<120){r=x;g=c;}else if(hue<180){g=c;b=x;}else if(hue<240){g=x;b=c;}else if(hue<300){r=x;b=c;}else{r=c;b=x;}return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));}
 public static void ToHsv(Color c,out double hue,out double saturation){double r=c.R/255.0,g=c.G/255.0,b=c.B/255.0;double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;hue=d==0?0:max==r?60*((g-b)/d%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);if(hue<0)hue+=360;saturation=max==0?0:d/max;}
}

sealed class ColourWheel : FrameworkElement {
 BitmapSource bitmap;double hue=20,saturation=0.45;public event Action<Color> Changed;
 public ColourWheel(){Focusable=true;Cursor=Cursors.Cross;System.Windows.Automation.AutomationProperties.SetName(this,"Colour wheel. Arrow keys change hue; Shift and arrows change saturation.");const int n=300;byte[] pixels=new byte[n*n*4];for(int y=0;y<n;y++)for(int x=0;x<n;x++){double dx=x-(n-1)/2.0,dy=y-(n-1)/2.0,r=Math.Sqrt(dx*dx+dy*dy),radius=n/2.0-2;Color c=ColourMath.FromHsv(Math.Atan2(dy,dx)*180/Math.PI,Math.Min(1,r/radius),1);int p=(y*n+x)*4;byte alpha=(byte)(Math.Max(0,Math.Min(1,radius-r))*255);pixels[p]=(byte)(c.B*alpha/255);pixels[p+1]=(byte)(c.G*alpha/255);pixels[p+2]=(byte)(c.R*alpha/255);pixels[p+3]=alpha;}bitmap=BitmapSource.Create(n,n,96,96,PixelFormats.Pbgra32,null,pixels,n*4);bitmap.Freeze();ToolTip="Drag to choose a colour. Arrow keys change hue; Shift + arrows change saturation.";}
 public void Select(Color c){ColourMath.ToHsv(c,out hue,out saturation);InvalidateVisual();}
 protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer(){return new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);double size=Math.Min(ActualWidth,ActualHeight)-12;double x=(ActualWidth-size)/2,y=(ActualHeight-size)/2;dc.DrawImage(bitmap,new Rect(x,y,size,size));double radius=size/2-2;var p=new Point(ActualWidth/2+Math.Cos(hue*Math.PI/180)*saturation*radius,ActualHeight/2+Math.Sin(hue*Math.PI/180)*saturation*radius);dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(190,23,25,28)),4),p,9,9);dc.DrawEllipse(null,new Pen(Brushes.White,2),p,8,8);if(IsKeyboardFocused)dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromRgb(243,148,133)),1),new Point(ActualWidth/2,ActualHeight/2),size/2+4,size/2+4);}
 void Pick(Point p){double radius=(Math.Min(ActualWidth,ActualHeight)-12)/2-2,dx=p.X-ActualWidth/2,dy=p.Y-ActualHeight/2;hue=(Math.Atan2(dy,dx)*180/Math.PI+360)%360;saturation=Math.Min(1,Math.Sqrt(dx*dx+dy*dy)/radius);InvalidateVisual();if(Changed!=null)Changed(ColourMath.FromHsv(hue,saturation,1));}
 protected override void OnMouseDown(MouseButtonEventArgs e){base.OnMouseDown(e);if(e.ChangedButton==MouseButton.Left){Focus();CaptureMouse();Pick(e.GetPosition(this));e.Handled=true;}}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)Pick(e.GetPosition(this));}
 protected override void OnMouseUp(MouseButtonEventArgs e){base.OnMouseUp(e);if(e.ChangedButton==MouseButton.Left)ReleaseMouseCapture();}
 protected override void OnKeyDown(KeyEventArgs e){if(e.Key==Key.Left||e.Key==Key.Right||e.Key==Key.Up||e.Key==Key.Down){int delta=e.Key==Key.Left||e.Key==Key.Down?-1:1;if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)saturation=Math.Max(0,Math.Min(1,saturation+delta*.02));else hue=(hue+delta*2+360)%360;InvalidateVisual();if(Changed!=null)Changed(ColourMath.FromHsv(hue,saturation,1));e.Handled=true;}base.OnKeyDown(e);}
 protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnGotKeyboardFocus(e);InvalidateVisual();}
 protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnLostKeyboardFocus(e);InvalidateVisual();}
}
