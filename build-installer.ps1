$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'SshFilePreview.csproj'
$stagePath = Join-Path $PSScriptRoot 'installer-stage'
$scriptPath = Join-Path $PSScriptRoot 'installer.iss'

$compilerCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
$compiler = if ($compilerCommand) { $compilerCommand.Source } else { $null }
if (-not $compiler) {
  $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
  $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
  $localAppData = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)
  $candidates = @(
    (Join-Path $localAppData 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe'),
    (Join-Path $programFiles 'Inno Setup 6\ISCC.exe')
  )
  $compiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $compiler) {
  throw 'Inno Setup 6 is required. Install it with: winget install --id JRSoftware.InnoSetup --exact'
}

if (Test-Path $stagePath) {
  Remove-Item -LiteralPath $stagePath -Recurse -Force
}

dotnet publish $projectPath `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output $stagePath `
  -p:DebugSymbols=false `
  -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
  throw "Self-contained publish failed with exit code $LASTEXITCODE"
}

Push-Location $PSScriptRoot
try {
  & $compiler $scriptPath
  if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
  }
}
finally {
  Pop-Location
}

Write-Host "Installer ready: $(Join-Path $PSScriptRoot 'installer-output\SSH-File-Preview-Setup-x64.exe')"
