$ErrorActionPreference = 'Stop'

dotnet publish (Join-Path $PSScriptRoot 'SshFilePreview.csproj') `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output (Join-Path $PSScriptRoot 'publish')

if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Ready: $PSScriptRoot\publish\SshFilePreview.exe"

& (Join-Path $PSScriptRoot 'install-shortcut.ps1')
