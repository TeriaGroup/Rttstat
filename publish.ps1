$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "publish\portable"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\Netpulse.App\Netpulse.App.csproj") -c Release -r win-x64 --self-contained true -o $out
Write-Host "Portable output: $out"
Write-Host "Compile installer/rttstat.iss with Inno Setup 6 after this publish."
