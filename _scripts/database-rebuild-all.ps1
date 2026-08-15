# För att göra .ps1-filen körbar, kör följande kommando i PowerShell (Behöver bara köras första gången):
# Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser


#If EFC tools needs update use:
#dotnet tool update --global dotnet-ef

# To execute:
# .\database-rebuild-all.ps1 databasename [sqlserver|mysql|postgresql] [docker|azure] [root|dbo|supusr|usr|gstusr] appsettingsFolder

# example:
# .\database-rebuild-all.ps1 sql-music sqlserver docker dbo ..\AppWebApi
# .\database-rebuild-all.ps1 sql-music sqlserver docker dbo ..\AppRazor
# .\database-rebuild-all.ps1 sql-music sqlserver docker dbo ..\AppMvc

param(
    [Parameter(Mandatory=$true)]
    [string]$DatabaseName,

    [Parameter(Mandatory=$true)]
    [ValidateSet("sqlserver", "mysql", "postgresql")]
    [string]$DatabaseType,
    
    [Parameter(Mandatory=$true)]
    [ValidateSet("docker", "azure")]
    [string]$DeploymentTarget,

    [Parameter(Mandatory=$true)]
    [ValidateSet("root", "dbo", "supusr", "usr", "gstusr")]
    [string]$DefaultDataUser,

    [Parameter(Mandatory=$true)]
    [string]$AppSettingsFolder
)

#Set Database Context
switch ($DatabaseType) {
    "sqlserver" { $DBContext = "SqlServerDbContext" }
    "mysql" { $DBContext = "mysqlDbContext" }
    "postgresql" { $DBContext = "PostgresDbContext" }
}

$AppSettingsFolder = Resolve-Path $AppSettingsFolder
$AppSettingsPath = Join-Path $AppSettingsFolder "appsettings.json"

#set UseDataSetWithTag to "<db_name>.<db_type>.<env>" in appsettings.json
$pattern = '"UseDataSetWithTag"\s*:\s*"[^"]*"'
$replacement = '"UseDataSetWithTag": "' + $DatabaseName + '.' + $DatabaseType + '.' + $DeploymentTarget + '"'
(Get-Content -Path $AppSettingsPath) -replace $pattern, $replacement | Set-Content -Path $AppSettingsPath

#set DefaultDataUser in appsettings.json
$Content = Get-Content $AppSettingsPath -Raw
$UpdatedContent = $Content -replace '"DefaultDataUser":\s*"[^"]*"', ('"DefaultDataUser": "' + $DefaultDataUser + '"')
Set-Content $AppSettingsPath $UpdatedContent

if ($DeploymentTarget -eq "docker") {
    #drop any database
    $env:EFC_AppSettingsFolder = $AppSettingsFolder
    dotnet ef database drop -f -c $DBContext -p ../DbContext -s ../DbContext
}

#remove any migration
Remove-Item -Recurse -Force ../DbContext/Migrations/$DBContext -ErrorAction SilentlyContinue

#make a full new migration
$env:EFC_AppSettingsFolder = $AppSettingsFolder
dotnet ef migrations add miInitial -c $DBContext -p ../DbContext -s ../DbContext -o ../DbContext/Migrations/$DBContext

#update the database from the migration
$env:EFC_AppSettingsFolder = $AppSettingsFolder
dotnet ef database update -c $DBContext -p ../DbContext -s ../DbContext

#to initialize the database you need to run the sql scripts
#../DbContext/SqlScripts/<db_type>/initDatabase.sql

