using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Configuration;
using Configuration.Extensions;
using Configuration.Options;

namespace DbContext.Extensions;

public static class DbContextDesignTimeExtensions
{
    public static DbContextOptionsBuilder ConfigureForDesignTime(
        this DbContextOptionsBuilder optionsBuilder, 
        Func<DbContextOptionsBuilder, string, DbContextOptionsBuilder> databaseOptions)
    {
        System.Console.WriteLine($"Executing DesignTimeConfigure...");
        
        var (configuration, databaseConnections) = CreateDesignTimeServices();
        
        var connection = GetDatabaseConnection(configuration, databaseConnections);

        optionsBuilder = databaseOptions(optionsBuilder, connection.DbConnectionString);
        System.Console.WriteLine($"DesignTimeConfigure completed successfully");
        System.Console.WriteLine($"Proceeding with migration.");
        System.Console.WriteLine($"   User: {connection.DbUserLogin}");
        System.Console.WriteLine($"   Database connection: {connection.DbConnection}");
        
        return optionsBuilder;
    }

    private static (IConfiguration configuration, DatabaseConnections databaseConnections) CreateDesignTimeServices()
    {
        //ASP.NET Core program.cs has not run by efc design-time, configure and create services as in program.cs

        // Get folder where appsettings.json is located from environment variable
        var appsettingsFolder = Environment.GetEnvironmentVariable("EFC_AppSettingsFolder")?? Directory.GetCurrentDirectory();
        System.Console.WriteLine($"   using appsettings.json in folder: {appsettingsFolder}");
        if (File.Exists(Path.Combine(appsettingsFolder, "appsettings.json")))
        {
            System.Console.WriteLine($"   appsettings.json: {Path.Combine(appsettingsFolder, "appsettings.json")}");
        }
        else
        {
            throw new FileNotFoundException($"Error: appsettings.json not found in folder: {appsettingsFolder}");
        }

        // Create a configuration
        System.Console.WriteLine($"   configuring");
        var conf = new ConfigurationBuilder();
        conf.AddSecrets(new HostingEnvironment { EnvironmentName = "Development" }, appsettingsFolder);

        System.Console.WriteLine($"   building configuring");
        var configuration = conf.Build();

        System.Console.WriteLine($"   creating services");
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions(); // Add the Options framework
        serviceCollection.AddDatabaseConnections(configuration);
        serviceCollection.AddSingleton<IConfiguration>(configuration);

        // Build the service provider and get the DbContext
        System.Console.WriteLine($"   retrieving services from serviceProvider");

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var configurationService = serviceProvider.GetRequiredService<IConfiguration>();
        System.Console.WriteLine($"   {nameof(IConfiguration)} retrieved");

        var databaseConnections = serviceProvider.GetRequiredService<DatabaseConnections>();

        System.Console.WriteLine($"   {nameof(DatabaseConnections)} retrieved");
        System.Console.WriteLine($"   secret source: {databaseConnections.SetupInfo.SecretSource}");
        System.Console.WriteLine($"   secret id: {databaseConnections.SetupInfo.SecretId}");
        System.Console.WriteLine($"   DataConnectionTag: {databaseConnections.SetupInfo.DataConnectionTag}");

        return (configurationService, databaseConnections);
    }

    private static DbConnectionDetailOptions GetDatabaseConnection(IConfiguration configuration, DatabaseConnections databaseConnections)
    {
        var connection = databaseConnections.GetDataConnectionDetails(configuration["DatabaseConnections:MigrationUser"]);
        if (connection.DbConnectionString == null)
        {
            throw new InvalidDataException($"Error: Connection string for {connection.DbConnection}, {connection.DbUserLogin} not set");
        }

        return connection;
    }
}
