$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$isPackaged = (Test-Path -LiteralPath (Join-Path $projectRoot 'PhotoSense.exe')) -or (Test-Path -LiteralPath (Join-Path $projectRoot 'PixelPatchStudio.exe'))
$workRoot = if ($isPackaged) { Join-Path $env:TEMP 'PixelPatchStudio\realesrgan-download' } else { Join-Path $projectRoot 'work\realesrgan-download' }
$toolsRoot = if ($isPackaged) { Join-Path $projectRoot 'tools' } else { Join-Path $projectRoot 'outputs\tools' }
$archive = Join-Path $workRoot 'realesrgan-ncnn-vulkan-windows.zip'
$url = 'https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip'

New-Item -ItemType Directory -Force -Path $workRoot, $toolsRoot | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Write-Host 'Downloading the portable Windows build from the official Real-ESRGAN GitHub release...'
Invoke-WebRequest -Uri $url -OutFile $archive
Expand-Archive -LiteralPath $archive -DestinationPath $workRoot -Force

$executable = Get-ChildItem -LiteralPath $workRoot -Recurse -Filter 'realesrgan-ncnn-vulkan.exe' | Select-Object -First 1
if (-not $executable) { throw 'realesrgan-ncnn-vulkan.exe was not found in the downloaded archive.' }
$bundleRoot = $executable.Directory.FullName
Copy-Item -Path (Join-Path $bundleRoot '*') -Destination $toolsRoot -Recurse -Force
Write-Host ('REALESRGAN_OK ' + $toolsRoot)
