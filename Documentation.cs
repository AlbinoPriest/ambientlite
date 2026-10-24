using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

sealed partial class LightWindow {
 readonly bool renderingDocumentation=Array.IndexOf(Environment.GetCommandLineArgs(),"--render-docs")>=0;
 public void RenderDocumentation(string directory){
  Directory.CreateDirectory(directory);
  Navigate("Lighting");window.UpdateLayout();SaveVisual(window.Content as FrameworkElement,Path.Combine(directory,"screen-sync.png"));
  Navigate("Colour");ChooseColour((Color)ColorConverter.ConvertFromString("#FF9A88"),"Coral");window.UpdateLayout();SaveVisual(window.Content as FrameworkElement,Path.Combine(directory,"static-colour.png"));
  OpenQuick();flyout.UpdateLayout();SaveVisual(flyout.Content as FrameworkElement,Path.Combine(directory,"quick-controls.png"));
  HideQuick();Navigate("Startup");window.UpdateLayout();SaveVisual(window.Content as FrameworkElement,Path.Combine(directory,"startup.png"));
  Navigate("Setup");window.UpdateLayout();SaveVisual(window.Content as FrameworkElement,Path.Combine(directory,"strip-setup.png"));
  exiting=true;window.Close();
 }
 static void SaveVisual(FrameworkElement visual,string path){visual.UpdateLayout();int width=(int)Math.Ceiling(visual.ActualWidth),height=(int)Math.Ceiling(visual.ActualHeight);var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(path))png.Save(stream);}
}
