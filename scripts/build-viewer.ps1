$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot/..
try {
  Push-Location viewer
  # Use 'npm install' (incremental) instead of 'npm ci' so we don't try to
  # rmdir node_modules on every build — that hits EBUSY when an editor /
  # antivirus / indexer is touching files in node_modules. CI may want
  # to override this back to 'npm ci' via env var if reproducibility matters.
  if (-not (Test-Path node_modules)) {
    npm install --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
  }
  npm run build
  if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
  Pop-Location
  Write-Host "viewer build -> src/MarkdownReader/Resources/viewer"
} finally { Pop-Location }
