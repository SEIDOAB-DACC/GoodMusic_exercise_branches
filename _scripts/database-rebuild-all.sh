#!/bin/bash
#To make the .sh file executable
#sudo chmod +x ./database-rebuild-all.sh

#If EFC tools needs update use:
#dotnet tool update --global dotnet-ef

# To execute:
# ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql] [docker|azure] [root|dbo|supusr|usr|gstusr] [appsettingsFolder]

# example:
# ./database-rebuild-all.sh sql-music sqlserver docker dbo ../AppWebApi
# ./database-rebuild-all.sh sql-music sqlserver docker dbo ../AppRazor
# ./database-rebuild-all.sh sql-music sqlserver docker dbo ../AppMvc

# Exit immediately if any command fails
set -e

if [[ -z "$1" ]]; then
    printf "\nMissing parameters:\n  ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql] [docker|azure] [appsettingsFolder]\n"
    exit 1
fi

#Set Database Context
if [[ $2 == "sqlserver" ]]; then
    DBContext="SqlServerDbContext"

elif [[ $2 == "mysql" ]]; then
    DBContext="mysqlDbContext"

elif [[ $2 == "postgresql" ]]; then
    DBContext="PostgresDbContext"

else
    printf "\nWrong or missing parameters:\n  ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql] [docker|azure] [appsettingsFolder]\n"
    exit 1;
fi

if [[ $3 != "docker" && $3 != "azure" ]]; then
    printf "\nWrong or missing parameters:\n  ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql] [docker|azure] [appsettingsFolder]\n"
    exit 1
fi

if [[ $4 != "root" && $4 != "dbo" && $4 != "supusr" && $4 != "usr" && $4 != "gstusr" ]]; then
    printf "\nWrong or missing parameters:\n  ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql] [docker|azure] [appsettingsFolder]\n"
    exit 1
fi

if [[ -z "$5" ]]; then
    printf "\nMissing parameters:\n  ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql] [docker|azure] [appsettingsFolder]\n"
    exit 1
fi

# folder that contains appsettings.json
AppSettingsFolder=$(realpath "$5")

#set UseDataSetWithTag to "<db_name>.<db_type>.<env>" in appsettings.json
sed -i '' 's/"UseDataSetWithTag":[[:space:]]*"[^"]*"/"UseDataSetWithTag": "'$1'.'$2'.'$3'"/g' $AppSettingsFolder/appsettings.json

#set DefaultDataUser to "dbo"
sed -i '' 's/"DefaultDataUser":[[:space:]]*"[^"]*"/"DefaultDataUser": "'$4'"/g' $AppSettingsFolder/appsettings.json

if [[ $3 == "docker" ]]; then
    #drop any database
    export EFC_AppSettingsFolder="$AppSettingsFolder"
    dotnet ef database drop -f -c $DBContext -p ../DbContext -s ../DbContext
fi

#remove any migration
rm -rf ../DbContext/Migrations/$DBContext

#make a full new migration
export EFC_AppSettingsFolder="$AppSettingsFolder"
dotnet ef migrations add miInitial -c $DBContext -p ../DbContext -s ../DbContext -o ../DbContext/Migrations/$DBContext

#update the database from the migration
export EFC_AppSettingsFolder="$AppSettingsFolder"
dotnet ef database update -c $DBContext -p ../DbContext -s ../DbContext

#to initialize the database you need to run the sql scripts
#../DbContext/SqlScripts/<db_type>/initDatabase.sql

