param([string]$DeviceConfig)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /out:SmokeTests.exe SmokeTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    if ($DeviceConfig) { & .\SmokeTests.exe .\dist\CardReaderGuard.exe $DeviceConfig }
    else { & .\SmokeTests.exe .\dist\CardReaderGuard.exe }
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    if (!(Test-Path -LiteralPath '.\dist\CardReaderGuard.exe.config')) { throw 'High DPI runtime configuration is missing.' }
    $dpiCheck = Start-Process -FilePath '.\dist\CardReaderGuard.exe' -ArgumentList '--dpi-check' -PassThru -Wait -WindowStyle Hidden
    if ($dpiCheck.ExitCode -ne 0) { throw 'Per-monitor DPI awareness check failed.' }
    Write-Output 'PASS per-monitor DPI awareness in the packaged application'
} finally { Pop-Location }
