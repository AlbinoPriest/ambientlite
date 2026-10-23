$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testExe = Join-Path $PSScriptRoot 'SessionTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /optimize+ /platform:x64 "/out:$testExe" (Join-Path $repo 'AppEnvironment.cs') (Join-Path $PSScriptRoot 'SessionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Session regression test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Session regression test failed.' }
