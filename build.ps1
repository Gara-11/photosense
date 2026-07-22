param(
    [switch]$SkipTests,
    [string]$OutputName = 'PhotoSense.exe'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src'
$outputRoot = Join-Path $projectRoot 'outputs'
$workRoot = Join-Path $projectRoot 'work\build'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Windows C# compiler not found. Enable .NET Framework 4.x.'
}

New-Item -ItemType Directory -Force -Path $outputRoot, $workRoot | Out-Null
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object { $_.FullName }
$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.IO.Compression.dll',
    'System.IO.Compression.FileSystem.dll',
    'System.Net.Http.dll',
    'System.Security.dll',
    'System.Web.Extensions.dll',
    'System.Windows.Forms.dll'
)
$referenceArgs = $references | ForEach-Object { '/reference:' + $_ }
$common = @('/nologo', '/optimize+', '/platform:anycpu', '/warn:4') + $referenceArgs
$logoAsset = Join-Path $projectRoot 'assets\PixelPatchLogo.jpg'
if (-not (Test-Path -LiteralPath $logoAsset)) { throw 'Brand logo asset is missing.' }
$resourceArgs = @('/resource:' + $logoAsset + ',PixelPatchStudio.PixelPatchLogo.jpg')

$testExe = Join-Path $workRoot 'PhotoSense.Tests.exe'
& $compiler @common @resourceArgs '/target:exe' ('/out:' + $testExe) @sources
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }

if (-not $SkipTests) {
    & $testExe --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Self-test failed.' }
}

$appExe = Join-Path $outputRoot $OutputName
& $compiler @common @resourceArgs '/target:winexe' ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')) ('/out:' + $appExe) @sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }

Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $outputRoot 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'download-realesrgan.ps1') -Destination (Join-Path $outputRoot 'Install-RealESRGAN.ps1') -Force
Write-Host ('BUILD_OK ' + $appExe)
