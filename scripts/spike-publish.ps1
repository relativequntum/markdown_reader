$ErrorActionPreference = 'Stop'
$proj = 'src/MarkdownReader/MarkdownReader.csproj'
dotnet publish $proj -c Release -r win-x64 `
  -p:PublishAot=true `
  -p:PublishSingleFile=false `
  -o publish/aot
Write-Host '--- aot output ---'
Get-ChildItem publish/aot | Format-Table Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB,2)}}

dotnet publish $proj -c Release -r win-x64 `
  -p:PublishSingleFile=true `
  -p:PublishReadyToRun=true `
  -p:PublishTrimmed=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:SelfContained=true `
  -o publish/r2r
Get-ChildItem publish/r2r | Format-Table Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB,2)}}
