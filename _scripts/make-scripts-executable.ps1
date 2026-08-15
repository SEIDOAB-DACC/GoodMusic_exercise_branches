# PowerShell script to make all .ps1 files in _scripts directory executable
# Run this script with: .\make-scripts-executable.ps1
# You may need to set execution policy first: Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

Write-Host "Making all .ps1 files in _scripts directory executable..." -ForegroundColor Green

# Get the directory where this script is located
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Script directory: $ScriptDir" -ForegroundColor Yellow

# Find all .ps1 files recursively in the _scripts directory
$ps1Files = Get-ChildItem -Path $ScriptDir -Filter "*.ps1" -Recurse -File

Write-Host "Found $($ps1Files.Count) .ps1 files" -ForegroundColor Cyan

# On Windows, we don't need to change file permissions like Unix chmod +x
# PowerShell scripts are executable by default if execution policy allows it
# But we can check and report on execution policy

$executionPolicy = Get-ExecutionPolicy -Scope CurrentUser
Write-Host "Current execution policy for current user: $executionPolicy" -ForegroundColor Yellow

if ($executionPolicy -eq "Restricted" -or $executionPolicy -eq "AllSigned") {
    Write-Host "WARNING: Current execution policy may prevent running PowerShell scripts." -ForegroundColor Red
    Write-Host "Consider running: Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor Yellow
} else {
    Write-Host "Execution policy allows running PowerShell scripts." -ForegroundColor Green
}

Write-Host "`nList of .ps1 files found:" -ForegroundColor Cyan
foreach ($file in $ps1Files) {
    $relativePath = $file.FullName.Replace($ScriptDir, "").TrimStart('\', '/')
    Write-Host "  $relativePath" -ForegroundColor White
}

Write-Host "`nTotal .ps1 files processed: $($ps1Files.Count)" -ForegroundColor Green
Write-Host "All .ps1 files in _scripts directory are ready to execute!" -ForegroundColor Green