#!/bin/bash
#To make the .sh file executable
#sudo chmod +x ./database-rebuild-all.sh

#If EFC tools needs update use:
#dotnet tool update --global dotnet-ef

# To execute:
# ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql]

# example:
# ./database-rebuild-all.sh sql-music sqlserver 
# ./database-rebuild-all.sh sql-music sqlserver 
# ./database-rebuild-all.sh sql-music sqlserver

# Exit immediately if any command fails
set -e

if [[ -z "$1" ]]; then
    printf "\nMissing parameters:\n  ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql]\n"
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
    printf "\nWrong or missing parameters:\n  ./database-rebuild-all.sh databasename [sqlserver|mysql|postgresql]\n"
    exit 1;
fi


#drop any database
dotnet ef database drop -f -c $DBContext -p ../DbContext -s ../DbContext

#remove any migration
rm -rf ../DbContext/Migrations/$DBContext

#make a full new migration
dotnet ef migrations add miInitial -c $DBContext -p ../DbContext -s ../DbContext -o ../DbContext/Migrations/$DBContext

#update the database from the migration
dotnet ef database update -c $DBContext -p ../DbContext -s ../DbContext

