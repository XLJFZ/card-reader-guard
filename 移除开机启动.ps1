$startup = [Environment]::GetFolderPath('Startup')
$shortcut = Join-Path $startup 'CardReaderGuard.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'stop.flag'), '')
Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CardReaderGuard' -ErrorAction SilentlyContinue
