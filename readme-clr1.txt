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

3. Use Azure Data Studio to execute SQL script DbContext/SqlScripts/<db_type>/azure/initDatabase.sql

4. Run AppWebApi with or without debugger

   Without debugger:   
   Open a Terminal in folder AppWebApi run: 
   dotnet run -lp https 
   open url: https://localhost:7066/swagger

   Verify your can execute endpoint Admin/Environment and Guest/Info

5. Use endpoint Admin/Seed to seed the database, Admin/RemoveSeed to remove the seed
   Verify database seed with endpoint Guest/Info

6. You can now use and play with all endpoints
