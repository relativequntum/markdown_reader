$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot/.."
Push-Location $repoRoot
try {
  $out = Join-Path $repoRoot "publish/win-x64"
  if (Test-Path $out) { Remove-Item $out -Recurse -Force }

  Write-Host "==> Building viewer..."
  & "$PSScriptRoot/build-viewer.ps1"
  if ($LASTEXITCODE -ne 0) { throw "viewer build failed" }

  Write-Host "==> Publishing native shell..."
  # Spike decision (see docs/spike-2026-05-12-aot.md): WPF + trim is blocked
  # by NETSDK1168 on .NET 8; AOT therefore unavailable. Use SingleFile + R2R
  # + SelfContained without trim. Expected size ~170 MB.
  dotnet publish src/MarkdownReader `
    -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $out
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

  Write-Host "==> Output:"
  Get-ChildItem $out | Where-Object { -not $_.PSIsContainer } |
    Sort-Object Length -Descending |
    Select-Object -First 10 Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB,2)}} |
    Format-Table -AutoSize

  $exe = Get-Item (Join-Path $out "MarkdownReader.exe")
  Write-Host ""
  Write-Host "Publish complete: $($exe.FullName) ($([math]::Round($exe.Length/1MB,1)) MB)"
} finally {
  Pop-Location
}
