$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# 编译器不可用。' }
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$runtimeFacade = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll'
$metadata = Join-Path $env:WINDIR 'System32\WinMetadata'
$icon = Join-Path $PSScriptRoot 'assets\TileGenie.ico'
& $compiler "/reference:$runtimeFacade" "/reference:$metadata\Windows.ApplicationModel.winmd" "/reference:$metadata\Windows.UI.winmd" "/reference:$metadata\Windows.Foundation.winmd" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$icon" "/resource:$icon,TileMosaic.TileGenie.ico" "/out:$output\TileGenie.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:Microsoft.CSharp.dll @sources
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $output '使用说明.md') -ErrorAction SilentlyContinue
Write-Output "Build OK: $output\TileGenie.exe"
