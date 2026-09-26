param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$version = '1.0.4191.47'
$expected = 'F492BBF547D0DA329553B6727435B677579B1E9F91CC9E4A1AD029366D5F23D0'
$cache = Join-Path $PSScriptRoot '.deps'
$package = Join-Path $cache "webview2-$version.zip"
$sdk = Join-Path $cache "webview2-$version"
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $cache,$output -Force | Out-Null
if (-not (Test-Path -LiteralPath $package)) {
    if ($Offline) { throw 'WebView2 SDK cache missing. Run build.ps1 online once.' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile ($package + '.download')
    if ((Get-FileHash -LiteralPath ($package + '.download') -Algorithm SHA256).Hash -ne $expected) { throw 'WebView2 SDK hash mismatch' }
    Move-Item -LiteralPath ($package + '.download') -Destination $package
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expected) { throw 'WebView2 SDK cache modified' }
# Extract the pinned package again so an altered extracted cache cannot affect the build.
Expand-Archive -LiteralPath $package -DestinationPath $sdk -Force
$runtime = Join-Path $output 'browser\sdk'
New-Item -ItemType Directory -Path $runtime,(Join-Path $runtime 'x86'),(Join-Path $runtime 'x64') -Force | Out-Null
foreach ($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')) { Copy-Item -LiteralPath (Join-Path $sdk "lib\net462\$name") -Destination $runtime -Force }
foreach ($arch in @('x86','x64')) { Copy-Item -LiteralPath (Join-Path $sdk "runtimes\win-$arch\native\WebView2Loader.dll") -Destination (Join-Path $runtime $arch) -Force }
foreach ($name in @('LICENSE.txt','NOTICE.txt')) { Copy-Item -LiteralPath (Join-Path $sdk $name) -Destination $runtime -Force }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Sort-Object Name | Select-Object -ExpandProperty FullName)
$exe = Join-Path $output '中文游戏助手-开源版.exe'
& $compiler /nologo /target:winexe /optimize+ "/out:$exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Data.dll /reference:System.IO.Compression.dll "/reference:$runtime\Microsoft.Web.WebView2.Core.dll" "/reference:$runtime\Microsoft.Web.WebView2.WinForms.dll" $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
foreach ($name in @('LICENSE','NOTICE.md','README.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $output -Force }
Write-Output "Built: $exe"
