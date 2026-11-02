$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler not found.' }
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Path dist -Force | Out-Null
    & $compiler /nologo /target:winexe /optimize+ /platform:x64 /win32manifest:app.manifest /win32icon:Assets\App.ico /resource:Assets\App.ico,CardReaderGuard.App.ico /reference:System.Management.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:dist\CardReaderGuard.exe ReaderGuard.cs SetupWizard.cs BindingStore.cs Manager.cs UiControls.cs AssemblyInfo.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    Get-ChildItem -File -Filter '*.vbs' | Copy-Item -Destination dist -Force
    Get-ChildItem -File -Filter '*.ps1' | Where-Object Name -notin @('build.ps1','test.ps1') | Copy-Item -Destination dist -Force
    Copy-Item -LiteralPath 'Assets\App.ico' -Destination 'dist\CardReaderGuard.ico' -Force
    Copy-Item -LiteralPath 'README.md','LICENSE' -Destination dist -Force
    Copy-Item -LiteralPath 'CardReaderGuard.exe.config' -Destination dist -Force
    $excluded = '^(device\.|status\.log|stop\.flag|manage\.flag|appearance\.txt$|SHA256SUMS\.txt$)'
    $package = Get-ChildItem dist -File | Where-Object { $_.Name -notmatch $excluded } | Sort-Object Name
    $sums = $package | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name }
    $sumPath = Join-Path $PWD 'dist\SHA256SUMS.txt'
    [IO.File]::WriteAllLines($sumPath, $sums, (New-Object System.Text.UTF8Encoding($false)))
    $archive = @($package.FullName) + $sumPath
    Compress-Archive -LiteralPath $archive -DestinationPath CardReaderGuard-v0.2.3-win-x64.zip -Force
} finally { Pop-Location }
