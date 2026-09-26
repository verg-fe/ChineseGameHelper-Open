$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$release = Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Path $release -Force | Out-Null
function Write-Package([string]$name, [object[]]$entries) {
    $target = Join-Path $release $name
    $stream = [IO.File]::Open($target,[IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in $entries) {
            if (-not (Test-Path -LiteralPath $item.Path -PathType Leaf)) { throw "Missing package file: $($item.Path)" }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$item.Path,$item.Name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
    Write-Output $target
}
$source = @()
foreach ($name in @('README.md','LICENSE','NOTICE.md','.gitignore','build.ps1','test.ps1','package.ps1')) {
    $source += [pscustomobject]@{Path=(Join-Path $PSScriptRoot $name);Name=$name}
}
foreach ($dir in @('src','docs')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $dir) -File -Recurse) {
        if ($file.Extension -notin @('.cs','.md','.json')) { throw "Unexpected source file: $($file.Name)" }
        $source += [pscustomobject]@{Path=$file.FullName;Name=$file.FullName.Substring($PSScriptRoot.Length+1).Replace('\','/')}
    }
}
Write-Package 'ChineseGameHelper-Open-source.zip' $source
# Explicit allowlist: no plugin DLL, runtime, weights, cache, test reports or screenshots.
$portable = @()
foreach ($name in @('中文游戏助手-开源版.exe','README.md','LICENSE','NOTICE.md','browser/sdk/Microsoft.Web.WebView2.Core.dll','browser/sdk/Microsoft.Web.WebView2.WinForms.dll','browser/sdk/x86/WebView2Loader.dll','browser/sdk/x64/WebView2Loader.dll','browser/sdk/LICENSE.txt','browser/sdk/NOTICE.txt')) {
    $portable += [pscustomobject]@{Path=(Join-Path (Join-Path $PSScriptRoot 'dist') $name);Name=$name}
}
Write-Package 'ChineseGameHelper-Open-portable.zip' $portable
$hashes = foreach ($name in @('ChineseGameHelper-Open-source.zip','ChineseGameHelper-Open-portable.zip')) {
    $hash = Get-FileHash -LiteralPath (Join-Path $release $name) -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $name"
}
$hashes | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding UTF8
[pscustomobject]@{sourceFiles=$source.Count;portableFiles=$portable.Count;sourcePayloads=0;neuralRuntimePayloads=0;models=0;note='Allowlisted packaging. WebView2 SDK is an external build dependency with its original notices.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $release 'package-audit.json') -Encoding UTF8
