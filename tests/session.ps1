$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testExe = Join-Path $PSScriptRoot 'SessionTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /optimize+ /platform:x64 "/out:$testExe" "/reference:$framework\System.Drawing.dll" (Join-Path $repo 'AppEnvironment.cs') (Join-Path $repo 'ShutdownLighting.cs') (Join-Path $repo 'LightingFrames.cs') (Join-Path $repo 'ColourTrace.cs') (Join-Path $PSScriptRoot 'SessionTests.cs') (Join-Path $PSScriptRoot 'ShutdownTests.cs') (Join-Path $PSScriptRoot 'LightingTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Session regression test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Session regression test failed.' }
