# az-access.ps1
# Usage: .\test-ssh-connection.ps1 192.168.68.53

param (
    [Parameter(Mandatory = $true)]
    [string]$DockerHost
)

Write-Host "Pinging $DockerHost" -ForegroundColor Yellow
ping $DockerHost

Write-Host "`n`nTesting SSH connection to $DockerHost" -ForegroundColor Yellow
ssh -o StrictHostKeyChecking=no -p 22 martin@$DockerHost "echo 'SSH connection successful'"

Write-Host "`n`nListing Docker containers on $DockerHost" -ForegroundColor Yellow
ssh martin@$DockerHost "/usr/local/bin/docker ps"

