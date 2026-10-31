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


# A separate, versioned companion package is consumed by EmberRGB's optional updater.
# Keep the ordinary standalone ZIP/installer above unchanged.
$companionVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $PSScriptRoot 'AmbientLite.exe')).Version.ToString(3)
$companionFolder=Join-Path $destination ('AmbientLite-'+$companionVersion+'-companion')
New-Item -ItemType Directory -Force -Path $companionFolder | Out-Null
foreach($file in @('AmbientLite.exe','README.md','LICENSE','SwooshDrop-LICENSE.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $companionFolder -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $companionFolder -Recurse -Force
[IO.File]::WriteAllText((Join-Path $companionFolder 'installed.marker'),'')
$companionArchive=Join-Path $destination ('AmbientLite-'+$companionVersion+'-companion.zip')
Compress-Archive -LiteralPath $companionFolder -DestinationPath $companionArchive -Force
$companionFiles=[ordered]@{}
Get-ChildItem -LiteralPath $companionFolder -Recurse -File | Sort-Object FullName | ForEach-Object { $companionFiles[$_.FullName.Substring($companionFolder.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
$companionManifest=[ordered]@{Schema=1;Product='AmbientLite';Version=$companionVersion;MinimumHost='0.12.0';ControlProtocol=1;Archive=[IO.Path]::GetFileName($companionArchive);Sha256=(Get-FileHash -LiteralPath $companionArchive -Algorithm SHA256).Hash.ToLowerInvariant();Files=$companionFiles}
[IO.File]::WriteAllText((Join-Path $destination 'AmbientLite-companion-update.json'),($companionManifest|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
Get-Item -LiteralPath $companionArchive,(Join-Path $destination 'AmbientLite-companion-update.json')
