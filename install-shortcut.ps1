$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'publish\SshFilePreview.exe'
if (-not (Test-Path -LiteralPath $exe)) {
  throw "Executable belum ada: $exe. Jalankan publish.ps1 dulu."
}

$programs = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
$shortcutPath = Join-Path $programs 'SSH File Preview.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = Split-Path -Parent $exe
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'Preview file kode dan gambar dari SSH'
$shortcut.Save()

Write-Host "Shortcut Windows Search dibuat: $shortcutPath"
