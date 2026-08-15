#!/bin/bash
#To make the .sh file executable
#sudo chmod +x ./nuget-update.sh

cd ../..
dotnet list package --outdated

#clear the local nuget cache
#dotnet nuget locals all --clear

#update efc
#dotnet tool update --global dotnet-ef