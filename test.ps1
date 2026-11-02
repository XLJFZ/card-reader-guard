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
    $bareRoot = Join-Path ([IO.Path]::GetTempPath()) ('CardReaderGuard-bare-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $bareRoot | Out-Null
    try {
        Copy-Item -LiteralPath '.\dist\CardReaderGuard.exe' -Destination $bareRoot
        $bareCheck = Start-Process -FilePath (Join-Path $bareRoot 'CardReaderGuard.exe') -ArgumentList '--dpi-check' -PassThru -Wait -WindowStyle Hidden
        if ($bareCheck.ExitCode -ne 0) { throw 'The executable lost per-monitor DPI awareness when run without CardReaderGuard.exe.config.' }
        Write-Output 'PASS executable keeps per-monitor DPI awareness without its config file'
    } finally { Remove-Item -LiteralPath $bareRoot -Recurse -Force -ErrorAction SilentlyContinue }
    $assembly = [regex]::Match((Get-Content -LiteralPath '.\AssemblyInfo.cs' -Raw), 'AssemblyFileVersion\("([\d.]+)"\)').Groups[1].Value
    $manifest = [regex]::Match((Get-Content -LiteralPath '.\app.manifest' -Raw), 'assemblyIdentity version="([\d.]+)"').Groups[1].Value
    if (!$assembly -or !$manifest) { throw 'Could not read version from AssemblyInfo.cs or app.manifest.' }
    if ($assembly -ne $manifest) { throw "app.manifest version $manifest does not match AssemblyInfo version $assembly." }
    Write-Output "PASS manifest version matches assembly version ($assembly)"
} finally { Pop-Location }
