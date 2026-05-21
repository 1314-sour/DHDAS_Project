$shipDir = "D:\DHDAS_Installation_Package"

# 1. Clean and Create Directory
if (Test-Path $shipDir) { 
    Write-Host "Cleaning old package..." -ForegroundColor Cyan
    Remove-Item -Recurse -Force $shipDir 
}
New-Item -ItemType Directory -Path "$shipDir\Plugins"

# 2. Publish Main Shell (Self-Contained for Win7)
Write-Host ">>> Publishing Shell (win-x64, Self-Contained)..." -ForegroundColor Cyan
dotnet publish src/4_Shell/DHDAS.App.Shell/DHDAS.App.Shell.csproj -c Release -r win-x64 --self-contained true -o "$shipDir"

# 3. Build Plugin
Write-Host ">>> Building Plugin..." -ForegroundColor Cyan
dotnet build src/3_Applications/DHDAS.Plugin.Waveform/DHDAS.Plugin.Waveform.csproj -c Release -r win-x64

# 4. Define Plugin Source Path (Note the win-x64 subfolder)
$pluginSource = "src/3_Applications/DHDAS.Plugin.Waveform/bin/Release/net6.0/win-x64/DHDAS.Plugin.Waveform.dll"

# 5. Check and Copy
if (Test-Path $pluginSource) {
    Copy-Item $pluginSource "$shipDir\Plugins\"
    Write-Host "------------------------------------------------" -ForegroundColor Green
    Write-Host "DEPLOY SUCCESSFUL!" -ForegroundColor Green
    Write-Host "Location: $shipDir" -ForegroundColor White
    Write-Host "Run DHDAS.App.Shell.exe to start." -ForegroundColor Yellow
    Write-Host "------------------------------------------------" -ForegroundColor Green
} else {
    Write-Host "!!! ERROR: Plugin DLL not found at: $pluginSource" -ForegroundColor Red
}