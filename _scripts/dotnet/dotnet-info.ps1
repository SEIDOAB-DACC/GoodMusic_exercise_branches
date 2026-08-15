# PowerShell script to display .NET information
# Run this script with: .\dotnet-info.ps1

Write-Host "Gathering .NET information..." -ForegroundColor Green

Write-Host "`n=== .NET Installation Info ===" -ForegroundColor Yellow
dotnet --info

Write-Host "`n=== Installed SDKs ===" -ForegroundColor Yellow
dotnet --list-sdks

Write-Host "`n=== Installed Runtimes ===" -ForegroundColor Yellow
dotnet --list-runtimes

Write-Host "`n.NET information gathering complete!" -ForegroundColor Green
