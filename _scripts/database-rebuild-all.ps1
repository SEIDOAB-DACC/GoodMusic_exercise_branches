# För att göra .ps1-filen körbar, kör följande kommando i PowerShell (Behöver bara köras första gången):
# Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser


#If EFC tools needs update use:
#dotnet tool update --global dotnet-ef

# To execute:
# .\database-rebuild-all.ps1 databasename [sqlserver|mysql|postgresql]

# example:
# .\database-rebuild-all.ps1 sql-music sqlserver

param(
    [Parameter(Mandatory=$true)]
    [string]$DatabaseName,

    [Parameter(Mandatory=$true)]
    [ValidateSet("sqlserver", "mysql", "postgresql")]
    [string]$DatabaseType,
)

#Set Database Context
switch ($DatabaseType) {
    "sqlserver" { $DBContext = "SqlServerDbContext" }
    "mysql" { $DBContext = "mysqlDbContext" }
    "postgresql" { $DBContext = "PostgresDbContext" }
}

#drop any database
dotnet ef database drop -f -c $DBContext -p ../DbContext -s ../DbContext

#remove any migration
Remove-Item -Recurse -Force ../DbContext/Migrations/$DBContext -ErrorAction SilentlyContinue

#make a full new migration
dotnet ef migrations add miInitial -c $DBContext -p ../DbContext -s ../DbContext -o ../DbContext/Migrations/$DBContext

#update the database from the migration
dotnet ef database update -c $DBContext -p ../DbContext -s ../DbContext

