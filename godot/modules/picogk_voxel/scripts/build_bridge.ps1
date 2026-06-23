# Builds all 3 LEAP71 libraries into PicogkGodotBridge and runs smoke test.
$ErrorActionPreference = "Stop"
$Bridge = Join-Path $PSScriptRoot "..\bridge"
Set-Location $Bridge
dotnet build -c Release
dotnet run -c Release --no-build
Write-Host "Bridge OK. Output:" (Join-Path $Bridge "bin\Release\net9.0")
