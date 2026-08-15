To create the AppWebApi

1. Create the database. With Terminal in folder _scripts 
   
   macOs
   ./database-rebuild-all.sh sql-music sqlserver docker root ../AppWebApi
   
   Windows
   ./database-rebuild-all.ps1 sql-music sqlserver docker root ../AppWebApi
   
   Ensure no errors from build, migration or database update


2. From Azure Data Studio you can now connect to the database
   Use connection string from user secrets:
   connection string corresponding to Tag
   "sql-music.<db_type>.docker.root"

3. Run AppWebApi with or without debugger

   Without debugger:   
   Open a Terminal in folder AppWebApi run: 
   dotnet run -lp https 
   open url: https://localhost:7066/swagger

   Verify your can execute endpoints
      Admin/Environment, Admin/Version and Admin/Log

4. Use From Azure Data Studio to explore the created database and it's schema 
   Notice that one table is implemented in the database

5. Use endpoint Admin/Seed to fill the database Quote table with content.
   Check the content using Azure Data Studio
