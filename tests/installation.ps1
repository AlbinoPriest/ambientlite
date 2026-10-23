# This integration check installs and uninstalls in a disposable Windows runner.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Run installation tests only in a disposable GitHub Actions Windows runner.' }
$repo = Split-Path $PSScriptRoot -Parent
$setup = Join-Path $repo 'dist\AmbientLiteSetup.exe'
$installed = Join-Path $env:LOCALAPPDATA 'Programs\AmbientLite'
$preferences = Join-Path $env:LOCALAPPDATA 'AmbientLite\settings.txt'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$app = Join-Path $installed 'AmbientLite.exe'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'AmbientLite.lnk'
function Invoke-Checked([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(30000)) { throw "Timed out: $File" }
    if ($process.ExitCode -ne 0) { throw "$File failed: $($process.ExitCode)" }
}
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
Invoke-Checked $setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
Assert (Test-Path -LiteralPath $app) 'App was not installed.'
Assert (Test-Path -LiteralPath (Join-Path $installed 'installed.marker')) 'Installed edition marker missing.'
Assert (Test-Path -LiteralPath $shortcut) 'Start menu shortcut missing.'
New-Item -ItemType Directory -Force -Path (Split-Path $preferences) | Out-Null
[IO.File]::WriteAllLines($preferences, @('\\.\DISPLAY1','15','37','65','41','15','True','1','#85FFD8','True','False'))
Invoke-Checked $app @('--startup-enable')
Assert ((Get-ItemPropertyValue $runKey AmbientLite) -eq ('"' + $app + '" --startup')) 'Startup command is not quoted or points at the wrong executable.'
Invoke-Checked $app @('--startup-disable')
Assert ($null -eq (Get-ItemProperty $runKey -Name AmbientLite -ErrorAction SilentlyContinue)) 'Startup toggle did not remove registration.'
Invoke-Checked $app @('--startup-enable')
$first = Start-Process -FilePath $app -ArgumentList '--startup' -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 3
$first.Refresh()
Assert (-not $first.HasExited) 'Tray startup exited unexpectedly.'
Assert ($first.MainWindowHandle -eq 0) 'Start-in-tray showed a main window.'
Invoke-Checked $app @('--startup')
$saved = [IO.File]::ReadAllText($preferences)
# Updating a running installation must stop it gracefully and retain choices.
Invoke-Checked $setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
Assert ($first.WaitForExit(10000)) 'Update did not close the running app.'
Assert (([IO.File]::ReadAllText($preferences)) -eq $saved) 'Update changed preferences.'
Assert (Test-Path -LiteralPath $shortcut) 'Start menu shortcut missing.'
Assert ((Get-ItemPropertyValue $runKey AmbientLite) -eq ('"' + $app + '" --startup')) 'Update removed startup preference.'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{20EE0678-1A5E-4872-91F6-70B9106B0B8A}_is1'
Assert (Test-Path $uninstallKey) 'Windows Installed apps entry missing.'
$active = Start-Process -FilePath $app -ArgumentList '--startup' -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 2
Invoke-Checked (Join-Path $installed 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
Assert ($active.WaitForExit(10000)) 'Uninstall left the app running.'
Assert (-not (Test-Path -LiteralPath $app)) 'Uninstall left the app executable.'
Assert (-not (Test-Path -LiteralPath $shortcut)) 'Uninstall left the shortcut.'
Assert (-not (Test-Path $uninstallKey)) 'Uninstall left the Installed apps entry.'
Assert ($null -eq (Get-ItemProperty $runKey -Name AmbientLite -ErrorAction SilentlyContinue)) 'Uninstall left Windows startup enabled.'
Assert (([IO.File]::ReadAllText($preferences)) -eq $saved) 'Uninstall removed or changed personal preferences.'

$portable = Join-Path $env:RUNNER_TEMP 'AmbientLite portable test'
Expand-Archive -LiteralPath (Join-Path $repo 'dist\AmbientLite-windows-x64.zip') -DestinationPath $portable
$portableApp = Join-Path $portable 'AmbientLite.exe'
$portableSettings = Join-Path $portable 'settings.txt'
Assert (-not (Test-Path (Join-Path $portable 'installed.marker'))) 'Portable archive contains an installed marker.'
[IO.File]::WriteAllLines($portableSettings, @('\\.\DISPLAY1','20','23','45','41','15','True','1','#FF9A88','True','False'))
Invoke-Checked $portableApp @('--startup-enable')
Assert ((Get-ItemPropertyValue $runKey AmbientLite) -eq ('"' + $portableApp + '" --startup')) 'Portable startup did not handle spaces in its path.'
$portableProcess = Start-Process -FilePath $portableApp -ArgumentList '--startup' -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 2
Assert (-not $portableProcess.HasExited) 'Portable startup failed.'
Invoke-Checked $portableApp @('--shutdown')
Assert ($portableProcess.WaitForExit(10000)) 'Portable shutdown failed.'
Assert (([IO.File]::ReadAllLines($portableSettings))[2] -eq '23') 'Portable app did not retain its local preferences.'
Assert (([IO.File]::ReadAllText($preferences)) -eq $saved) 'Portable app touched installed preferences.'
Invoke-Checked $portableApp @('--startup-disable')
Write-Output 'Install, startup, running update, running uninstall, and portable preference checks passed.'
