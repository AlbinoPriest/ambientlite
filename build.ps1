$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','System.Runtime.Serialization.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
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
 $arguments += (Join-Path $PSScriptRoot 'DesktopCapture.cs')
 $arguments += (Join-Path $PSScriptRoot 'CaptureDiagnostics.cs')
 $arguments += (Join-Path $PSScriptRoot 'ShutdownLighting.cs'),(Join-Path $PSScriptRoot 'ShutdownControls.cs')
 $arguments += (Join-Path $PSScriptRoot 'LightingFrames.cs'),(Join-Path $PSScriptRoot 'AmbientControlProtocol.cs'),(Join-Path $PSScriptRoot 'LocalControl.cs')
 & $compiler @arguments
 if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}

$testArguments = @('/main:AmbientControlChecks',('/out:'+(Join-Path $PSScriptRoot 'tests\ControlTests.exe'))) + @($arguments | Where-Object { $_ -notlike '/out:*' }) + @((Join-Path $PSScriptRoot 'tests\ControlTests.cs'))
& $compiler @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Control test build failed' }
& $compiler /nologo /target:exe /platform:x64 ('/reference:'+(Join-Path $framework 'System.Runtime.Serialization.dll')) ('/out:'+(Join-Path $PSScriptRoot 'tests\ControlProbe.exe')) (Join-Path $PSScriptRoot 'AmbientControlProtocol.cs') (Join-Path $PSScriptRoot 'tests\ControlProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Control probe build failed' }
