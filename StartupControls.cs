using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

sealed partial class LightWindow {
 bool startInTray=true,startLighting=true,refreshingStartup;
 CheckBox startWithWindows,trayAtStartup,lightingAtStartup;
 void SetupStartup(){
  startWithWindows=Get<CheckBox>("StartWithWindows");trayAtStartup=Get<CheckBox>("TrayAtStartup");lightingAtStartup=Get<CheckBox>("LightingAtStartup");
  refreshingStartup=true;
  try{startWithWindows.IsChecked=!renderingDocumentation&&StartupRegistration.Enabled;}catch(Exception ex){Text("StartupStatus",ex.Message);}
  trayAtStartup.IsChecked=startInTray;lightingAtStartup.IsChecked=startLighting;refreshingStartup=false;
  Text("StorageDetail",renderingDocumentation||AppEnvironment.Installed?"Installed for your Windows account. Preferences stay with you through updates.":"Portable edition. Keep this folder in place if you enable Windows startup.");
  if(Environment.GetCommandLineArgs().Contains("--startup")&&startInTray){window.Opacity=0;window.ShowInTaskbar=false;}
  startWithWindows.Checked+=(s,e)=>ChangeStartup(true);startWithWindows.Unchecked+=(s,e)=>ChangeStartup(false);
  trayAtStartup.Checked+=(s,e)=>SaveStartupChoices();trayAtStartup.Unchecked+=(s,e)=>SaveStartupChoices();
  lightingAtStartup.Checked+=(s,e)=>SaveStartupChoices();lightingAtStartup.Unchecked+=(s,e)=>SaveStartupChoices();
  window.Loaded+=async(s,e)=>{
   bool startup=Environment.GetCommandLineArgs().Contains("--startup");
   if(startup&&startInTray){window.Hide();window.Opacity=1;}
   if(!renderingDocumentation&&(Environment.GetCommandLineArgs().Contains("--start")||(startup&&startLighting)))await Start(false);
  };
 }
 void ChangeStartup(bool enabled){
  if(refreshingStartup||renderingDocumentation)return;
  try{StartupRegistration.SetEnabled(enabled);Text("StartupStatus",enabled?"All set. AmbientLite will open when you sign in.":"Windows startup is off. Open AmbientLite whenever you like.");}
  catch(Exception ex){refreshingStartup=true;startWithWindows.IsChecked=!enabled;refreshingStartup=false;Text("StartupStatus","Could not change Windows startup. "+ex.Message);}
 }
 void SaveStartupChoices(){if(refreshingStartup)return;startInTray=trayAtStartup.IsChecked==true;startLighting=lightingAtStartup.IsChecked==true;SaveIfValid();}
 public void Activate(){window.Dispatcher.BeginInvoke(new Action(Show));}
 public void RequestExit(){window.Dispatcher.BeginInvoke(new Action(async()=>await ExitApplication()));}
 async Task ExitApplication(){exiting=true;HideQuick();await Stop();window.Close();}
}
