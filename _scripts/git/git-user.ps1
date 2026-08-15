# PowerShell script to configure Git user settings
# Run this script with: .\git-user.ps1

Write-Host "Configuring Git user settings..." -ForegroundColor Green

# Set your email and name here
$email = "you@example.com"
$name = "Your Name"

Write-Host "Setting Git global user email to: $email" -ForegroundColor Yellow
git config --global user.email $email

Write-Host "Setting Git global user name to: $name" -ForegroundColor Yellow
git config --global user.name $name

Write-Host "`nCurrent Git configuration:" -ForegroundColor Cyan
git config --global user.email
git config --global user.name

Write-Host "`nGit user configuration complete!" -ForegroundColor Green
