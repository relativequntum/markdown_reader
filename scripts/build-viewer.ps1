$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot/..
try {
  Push-Location viewer
  npm ci
  if ($LASTEXITCODE -ne 0) { throw "npm ci failed" }
  npm run build
  if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
  Pop-Location
  Write-Host "viewer build -> src/MarkdownReader/Resources/viewer"
} finally { Pop-Location }
