$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$archive = Join-Path $destination 'AmbientLite-windows-x64.zip'
$files = @('AmbientLite.exe', 'AmbientLite.Diagnostics.exe', 'README.md', 'LICENSE', 'SwooshDrop-LICENSE.txt', 'docs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing package input: $file. Run build.ps1 first." }
}
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AmbientLite.exe') -Destination (Join-Path $destination 'AmbientLite.exe') -Force
$assets = @($archive, (Join-Path $destination 'AmbientLite.exe'))
$setup = Join-Path $destination 'AmbientLiteSetup.exe'
if (Test-Path -LiteralPath $setup) { $assets += $setup }
$checksums = foreach ($asset in $assets) {
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    $line = $hash + '  ' + [System.IO.Path]::GetFileName($asset)
    [System.IO.File]::WriteAllText($asset + '.sha256', $line + [Environment]::NewLine)
    $line
}
[System.IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'), $checksums)
Get-Item -LiteralPath $assets
