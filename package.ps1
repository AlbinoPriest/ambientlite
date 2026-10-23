$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$archive = Join-Path $destination 'AmbientLite-windows-x64.zip'
$files = @('AmbientLite.exe', 'AmbientLite.Diagnostics.exe', 'README.md', 'LICENSE', 'SwooshDrop-LICENSE.txt', 'docs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing package input: $file. Run build.ps1 first." }
}
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($archive + '.sha256', $hash + '  AmbientLite-windows-x64.zip' + [Environment]::NewLine)
Get-Item -LiteralPath $archive
