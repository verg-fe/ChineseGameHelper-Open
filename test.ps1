$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist\中文游戏助手-开源版.exe'
foreach ($test in @('--self-test','--stack-self-test','--legacy-self-test','--components-self-test')) {
    $process = Start-Process -FilePath $exe -ArgumentList $test -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Failed: $test (exit $($process.ExitCode))" }
    Write-Output "PASS $test"
}
