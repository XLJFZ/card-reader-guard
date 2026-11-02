$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler not found.' }
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Path dist -Force | Out-Null
    & $compiler /nologo /target:winexe /optimize+ /platform:x64 /reference:System.Management.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:dist\CardReaderGuard.exe ReaderGuard.cs SetupWizard.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    Copy-Item -LiteralPath '启动.vbs','停止.vbs','重新绑定.vbs','移除开机启动.ps1','README.md','LICENSE' -Destination dist -Force
    Compress-Archive -LiteralPath (Get-ChildItem dist -File | Where-Object Name -notin @('device.xml','status.log','stop.flag')).FullName -DestinationPath CardReaderGuard-v0.1.0-win-x64.zip -Force
} finally { Pop-Location }
