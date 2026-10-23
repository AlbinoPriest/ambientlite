$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
foreach ($kind in @('winexe','exe')) {
 $name = if ($kind -eq 'winexe') { 'AmbientLite.exe' } else { 'AmbientLite.Diagnostics.exe' }
 $arguments = @('/nologo','/optimize+','/platform:x64',('/target:'+$kind),('/out:'+(Join-Path $PSScriptRoot $name)),('/win32icon:'+(Join-Path $PSScriptRoot 'AmbientLite.ico'))) + $references
 $arguments += '/resource:'+(Join-Path $PSScriptRoot 'AmbientLite.xaml')+',Ambient.UI'
 $arguments += '/resource:'+(Join-Path $PSScriptRoot 'AmbientLite.ico')+',Ambient.Icon'
 $arguments += '/resource:'+(Join-Path $PSScriptRoot 'TrayFlyout.xaml')+',Ambient.Tray'
 $arguments += '/resource:'+(Join-Path $PSScriptRoot 'LICENSE')+',Ambient.License'
 $arguments += '/resource:'+(Join-Path $PSScriptRoot 'SwooshDrop-LICENSE.txt')+',Ambient.SwooshLicense'
 $arguments += (Join-Path $PSScriptRoot 'AmbientLite.cs'),(Join-Path $PSScriptRoot 'LightWindow.cs'),(Join-Path $PSScriptRoot 'ColourWheel.cs'),(Join-Path $PSScriptRoot 'QuickControls.cs'),(Join-Path $PSScriptRoot 'Documentation.cs')
 $arguments += (Join-Path $PSScriptRoot 'AppEnvironment.cs'),(Join-Path $PSScriptRoot 'StartupControls.cs')
 & $compiler @arguments
 if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
