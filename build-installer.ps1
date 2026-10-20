$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'SshFilePreview.csproj'
$stagePath = Join-Path $PSScriptRoot 'installer-stage'
$outputPath = Join-Path $PSScriptRoot 'installer-output'
$scriptPath = Join-Path $PSScriptRoot 'installer.iss'
$setupPath = Join-Path $outputPath 'SSH-File-Preview-Setup-x64.exe'

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

if (Test-Path -LiteralPath $stagePath) {
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

$applicationPath = Join-Path $stagePath 'SshFilePreview.exe'
if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
  throw "Published application not found: $applicationPath"
}

$version = '1.0.0-local'
if ($env:GITHUB_REF_NAME -match '^v(.+)$') {
  $version = $Matches[1]
}
if (Test-Path -LiteralPath $outputPath) {
  Remove-Item -LiteralPath $outputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

Push-Location $PSScriptRoot
try {
  & $compiler "/DAppVersion=$version" $scriptPath
  if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
  }
}
finally {
  Pop-Location
}

if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
  throw "Installer executable not found: $setupPath"
}

$hash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $setupPath)" |
  Set-Content -LiteralPath (Join-Path $outputPath 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Installer ready: $setupPath (version $version)"
