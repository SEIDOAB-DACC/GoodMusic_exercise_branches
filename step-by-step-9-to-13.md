# Step-by-step: from `9-dbcontext` to `13-schema-annotations`

Two things are achieved in this branch:

1. **A second entity (`Album`) is added** with a one-to-many relationship to `MusicGroup`, and EF Core schema annotations are used to control table names, schemas, keys, and navigation properties precisely.
2. **`Program.cs` startup wiring is refactored** into extension methods shared between the runtime application and the `dotnet ef` design-time tooling.

Work through the steps in order — each step depends on the ones before it.

---

## Step 1 — Update `Models/Interfaces.cs`

Add the `IAlbum` interface and add an `Albums` navigation property to `IMusicGroup`. This establishes the public contract before any implementation:

```csharp
namespace Models.Interfaces;

public enum MusicGenre { Rock, Blues, Jazz, Metal }

public interface IMusicGroup
{
    public Guid MusicGroupId { get; set; }
    public string Name { get; set; }
    public List<IAlbum> Albums { get; set; }    // ← new
}

public interface IAlbum                          // ← new
{
    public Guid AlbumId { get; set; }
    public string Name { get; set; }
    public IMusicGroup MusicGroup { get; set; }
}
```

---

## Step 2 — Update `Models/MusicGroup.cs`

Add the `Albums` navigation property to satisfy the updated `IMusicGroup` interface:

```csharp
//Model relationships
public virtual List<IAlbum> Albums { get; set; } = new List<IAlbum>();
```

The full file after the change:

```csharp
using Seido.Utilities.SeedGenerator;
using Models.Interfaces;

namespace Models;

public class MusicGroup : IMusicGroup, ISeed<MusicGroup>
{
    public virtual Guid MusicGroupId { get; set; }
    public virtual string Name { get; set; }
    public virtual MusicGenre Genre { get; set; }

    //Model relationships
    public virtual List<IAlbum> Albums { get; set; } = new List<IAlbum>();

    #region Constructors
    public MusicGroup() { }
    public MusicGroup(MusicGroup org)
    {
        Seeded = org.Seeded;
        MusicGroupId = org.MusicGroupId;
        Name = org.Name;
        Genre = org.Genre;
    }
    #endregion

    #region randomly seed this instance
    public virtual bool Seeded { get; set; } = false;
    public virtual MusicGroup Seed(SeedGenerator seedGenerator)
    {
        Seeded = true;
        MusicGroupId = Guid.NewGuid();
        Name = seedGenerator.MusicGroupName;
        Genre = seedGenerator.FromEnum<MusicGenre>();
        return this;
    }
    #endregion
}
```

---

## Step 3 — Create `Models/Album.cs`

Create a new file. This is a pure domain model — no EF Core attributes:

```csharp
using Seido.Utilities.SeedGenerator;
using Models.Interfaces;

namespace Models;

public class Album : IAlbum, ISeed<Album>
{
    public virtual Guid AlbumId { get; set; }
    public virtual string Name { get; set; }

    //Model relationships
    public virtual IMusicGroup MusicGroup { get; set; } = null;

    #region Constructors
    public Album() { }
    public Album(Album org)
    {
        this.Seeded = org.Seeded;
        this.AlbumId = org.AlbumId;
        this.Name = org.Name;
    }
    #endregion

    #region randomly seed this instance
    public virtual bool Seeded { get; set; } = false;
    public virtual Album Seed(SeedGenerator seedGenerator)
    {
        Seeded = true;
        AlbumId = Guid.NewGuid();
        Name = seedGenerator.MusicAlbumName;
        return this;
    }
    #endregion
}
```

---

## Step 4 — Update `DbModels/MusicGroupDbM.cs`

Add the `[Table]` schema annotation and the navigation properties for the relationship to `AlbumDbM`.

The key pattern here: because `Albums` is typed `List<IAlbum>` (an interface), EF Core cannot map it — so it is marked `[NotMapped]` and a concrete-typed sibling `AlbumsDbM` becomes the real EF Core navigation property. `[JsonIgnore]` prevents an infinite loop during serialisation.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;

namespace DbModels;

[Table("MusicGroups", Schema = "supusr")]
public class MusicGroupDbM : MusicGroup, ISeed<MusicGroupDbM>
{
    [Key]
    public override Guid MusicGroupId { get; set; }

    #region implementing entity Navigation properties when model is using interfaces in the relationships between models
    [NotMapped]
    public override List<IAlbum> Albums { get => AlbumsDbM?.ToList<IAlbum>(); set => new NotImplementedException(); }
    [JsonIgnore]
    public virtual List<AlbumDbM> AlbumsDbM { get; set; } = null;
    #endregion

    #region Constructors
    public MusicGroupDbM() : base()
    {
        MusicGroupId = Guid.NewGuid();
    }
    #endregion

    #region randomly seed this instance
    public override MusicGroupDbM Seed(SeedGenerator seedGenerator)
    {
        base.Seed(seedGenerator);
        return this;
    }
    #endregion
}
```

---

## Step 5 — Create `DbModels/AlbumDbM.cs`

Create a new file. The same `[NotMapped]` + concrete-sibling pattern is used for the back-reference to `MusicGroupDbM`. `[Required]` on `MusicGroupDbM` tells EF Core the foreign key is non-nullable:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;

namespace DbModels;

[Table("Albums", Schema = "supusr")]
public class AlbumDbM : Album, ISeed<AlbumDbM>
{
    [Key]
    public override Guid AlbumId { get; set; }

    [Required]
    public override string Name { get; set; }

    #region implementing entity Navigation properties when model is using interfaces in the relationships between models
    [NotMapped]
    public override IMusicGroup MusicGroup { get => MusicGroupDbM; set => new NotImplementedException(); }
    [JsonIgnore]
    [Required]
    public virtual MusicGroupDbM MusicGroupDbM { get; set; } = null;
    #endregion

    #region Constructors
    public AlbumDbM() { }
    #endregion

    #region randomly seed this instance
    public override AlbumDbM Seed(SeedGenerator sgen)
    {
        base.Seed(sgen);
        return this;
    }
    #endregion
}
```

---

## Step 6 — Create five extension files in `Configuration/Extensions/`

These files extract the inline DI registrations from `Program.cs` into named, reusable methods. Create each file:

### `Configuration/Extensions/SecretsExtensions.cs`

Loads `appsettings.json` and user secrets (or Azure Key Vault in production). Called by both `Program.cs` at runtime and by `DbContextDesignTimeExtensions` during migrations:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Configuration.Extensions;

public static class SecretsExtensions
{
    const string _appsettingfile = "appsettings.json";

    public static IConfigurationBuilder AddSecrets(this IConfigurationBuilder config,
        IHostEnvironment environment, string appsettingsFolder = null)
    {
        appsettingsFolder ??= Directory.GetCurrentDirectory();
#if DEBUG
        config.SetBasePath(appsettingsFolder)
              .AddJsonFile(_appsettingfile, optional: true, reloadOnChange: true);
#else
        config.SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile(_appsettingfile, optional: true, reloadOnChange: true);
#endif
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        var tempConfig = config.Build();
        string secretStorage = tempConfig.GetValue<string>("ApplicationSecrets:SecretStorage");
        Console.WriteLine($"Using Secret Storage: {secretStorage}");

        if (environment.IsDevelopment())
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            var assembly = System.Reflection.Assembly.Load("Configuration");
            config.AddUserSecrets(assembly);

            tempConfig = config.Build();

            var userSecretsIdAttribute = assembly.GetCustomAttributes(typeof(UserSecretsIdAttribute), false)
                .FirstOrDefault() as UserSecretsIdAttribute;
            Console.WriteLine($"User Secrets ID: {userSecretsIdAttribute?.UserSecretsId}");

            if (secretStorage == "UserSecrets")
                Console.WriteLine("Using User Secrets in Development environment.");
            else
                throw new InvalidOperationException("Invalid SecretStorage value. Use 'UserSecrets' or 'AzureKeyVault'.");
        }
        else
        {
            throw new InvalidOperationException("Invalid SecretStorage value. 'AzureKeyVault' for production is not implemented.");
        }

        return config;
    }
}
```

### `Configuration/Extensions/DatabaseExtensions.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Configuration.Options;

namespace Configuration.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabaseConnections(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.Configure<DbConnectionSetsOptions>(
            options => configuration.GetSection(DbConnectionSetsOptions.Position).Bind(options));
        serviceCollection.AddSingleton<DatabaseConnections>();
        return serviceCollection;
    }
}
```

### `Configuration/Extensions/EncryptionExtensions.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Configuration.Options;

namespace Configuration.Extensions;

public static class EncryptionExtensions
{
    public static IServiceCollection AddEncryptions(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.Configure<AesEncryptionOptions>(
            options => configuration.GetSection(AesEncryptionOptions.Position).Bind(options));
        serviceCollection.AddTransient<Encryptions>();
        return serviceCollection;
    }
}
```

### `Configuration/Extensions/LoggerExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configuration.Extensions;

public static class LoggerExtensions
{
    public static IServiceCollection AddInMemoryLogger(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ILoggerProvider, InMemoryLoggerProvider>();
        return serviceCollection;
    }
}
```

### `Configuration/Extensions/VersionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Configuration.Options;

namespace Configuration.Extensions;

public static class VersionExtensions
{
    public static IServiceCollection AddVersionInfo(this IServiceCollection serviceCollection)
    {
        serviceCollection.Configure<VersionOptions>(options => VersionOptions.ReadFromAssembly(options));
        return serviceCollection;
    }
}
```

---

## Step 7 — Create `DbContext/Extensions/DbContextExtensions.cs`

`AddUserBasedDbContext()` replaces the inline `AddDbContext` block in `Program.cs`. It reads `DefaultDataUser` from configuration, resolves the connection string via `DatabaseConnections`, and selects the correct EF Core provider:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Configuration;

namespace DbContext.Extensions;

public static class DbContextExtensions
{
    public static IServiceCollection AddUserBasedDbContext(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<MainDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var databaseConnections = serviceProvider.GetRequiredService<DatabaseConnections>();

            var userRole = configuration["DatabaseConnections:DefaultDataUser"];
            var conn = databaseConnections.GetDataConnectionDetails(userRole);

            if (databaseConnections.SetupInfo.DataConnectionServer == DatabaseServer.SQLServer)
                options.UseSqlServer(conn.DbConnectionString, o => o.EnableRetryOnFailure());
            else if (databaseConnections.SetupInfo.DataConnectionServer == DatabaseServer.MySql)
                options.UseMySql(conn.DbConnectionString, ServerVersion.AutoDetect(conn.DbConnectionString),
                    b => b.SchemaBehavior(Microting.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Translate,
                        (schema, table) => $"{schema}_{table}"));
            else if (databaseConnections.SetupInfo.DataConnectionServer == DatabaseServer.PostgreSql)
                options.UseNpgsql(conn.DbConnectionString);
            else
                throw new InvalidDataException($"DbContext for {databaseConnections.SetupInfo.DataConnectionServer} not existing");
        });

        return serviceCollection;
    }
}
```

---

## Step 8 — Create `DbContext/Extensions/DbContextDesignTimeExtensions.cs`

When `dotnet ef migrations add` runs, `Program.cs` is **not** executed. This extension rebuilds only the services needed to obtain a connection string — using the same `AddSecrets` and `AddDatabaseConnections` extensions as the runtime app.

The `EFC_AppSettingsFolder` environment variable (set by the rebuild scripts) tells it where to find `appsettings.json`:

```csharp
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
        Console.WriteLine("Executing DesignTimeConfigure...");

        var (configuration, databaseConnections) = CreateDesignTimeServices();
        var connection = GetDatabaseConnection(configuration, databaseConnections);

        optionsBuilder = databaseOptions(optionsBuilder, connection.DbConnectionString);

        Console.WriteLine("DesignTimeConfigure completed successfully");
        Console.WriteLine($"   User: {connection.DbUserLogin}");
        Console.WriteLine($"   Database connection: {connection.DbConnection}");

        return optionsBuilder;
    }

    private static (IConfiguration, DatabaseConnections) CreateDesignTimeServices()
    {
        var appsettingsFolder = Environment.GetEnvironmentVariable("EFC_AppSettingsFolder")
            ?? Directory.GetCurrentDirectory();

        Console.WriteLine($"   using appsettings.json in folder: {appsettingsFolder}");

        if (!File.Exists(Path.Combine(appsettingsFolder, "appsettings.json")))
            throw new FileNotFoundException($"Error: appsettings.json not found in folder: {appsettingsFolder}");

        var conf = new ConfigurationBuilder();
        conf.AddSecrets(new HostingEnvironment { EnvironmentName = "Development" }, appsettingsFolder);
        var configuration = conf.Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        serviceCollection.AddDatabaseConnections(configuration);
        serviceCollection.AddSingleton<IConfiguration>(configuration);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var databaseConnections = serviceProvider.GetRequiredService<DatabaseConnections>();

        Console.WriteLine($"   secret source: {databaseConnections.SetupInfo.SecretSource}");
        Console.WriteLine($"   DataConnectionTag: {databaseConnections.SetupInfo.DataConnectionTag}");

        return (serviceProvider.GetRequiredService<IConfiguration>(), databaseConnections);
    }

    private static DbConnectionDetailOptions GetDatabaseConnection(
        IConfiguration configuration, DatabaseConnections databaseConnections)
    {
        var connection = databaseConnections.GetDataConnectionDetails(
            configuration["DatabaseConnections:MigrationUser"]);

        if (connection.DbConnectionString == null)
            throw new InvalidDataException(
                $"Error: Connection string for {connection.DbConnection}, {connection.DbUserLogin} not set");

        return connection;
    }
}
```

---

## Step 9 — Update `DbContext/MainDbContext.cs`

Three changes:

1. Add `using DbContext.Extensions;`
2. Add a `#if DEBUG` property that exposes the active connection string with the password stripped (useful for debug endpoints)
3. Add `DbSet<AlbumDbM> Albums`
4. Delete the private `GetConnectionString()` method and replace each call with `ConfigureForDesignTime()`

Full file after changes:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Configuration;
using DbModels;
using Microsoft.Extensions.Hosting.Internal;
using DbContext.Extensions;

namespace DbContext;

public class MainDbContext : Microsoft.EntityFrameworkCore.DbContext
{
#if DEBUG
    // remove password from connection string in debug mode
    public string dbConnection => System.Text.RegularExpressions.Regex.Replace(
        this.Database.GetConnectionString() ?? "", @"(pwd|password)=[^;]*;?", "",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
#endif

    #region C# model of database tables
    public DbSet<MusicGroupDbM> MusicGroups { get; set; }
    public DbSet<AlbumDbM> Albums { get; set; }
    #endregion

    #region constructors
    public MainDbContext() { }
    public MainDbContext(DbContextOptions options) : base(options) { }
    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    #region DbContext for some popular databases
    public class SqlServerDbContext : MainDbContext
    {
        public SqlServerDbContext() { }
        public SqlServerDbContext(DbContextOptions options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder = optionsBuilder.ConfigureForDesignTime(
                    (options, connectionString) =>
                        options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure()));
            }
            base.OnConfiguring(optionsBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HaveColumnType("money");
            configurationBuilder.Properties<string>().HaveColumnType("varchar(200)");
            base.ConfigureConventions(configurationBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }

    public class MySqlDbContext : MainDbContext
    {
        public MySqlDbContext() { }
        public MySqlDbContext(DbContextOptions options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder = optionsBuilder.ConfigureForDesignTime(
                    (options, connectionString) =>
                        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                            b => b.SchemaBehavior(
                                Microting.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Translate,
                                (schema, table) => $"{schema}_{table}")));
            }
            base.OnConfiguring(optionsBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<string>().HaveColumnType("varchar(200)");
            base.ConfigureConventions(configurationBuilder);
        }
    }

    public class PostgresDbContext : MainDbContext
    {
        public PostgresDbContext() { }
        public PostgresDbContext(DbContextOptions options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder = optionsBuilder.ConfigureForDesignTime(
                    (options, connectionString) => options.UseNpgsql(connectionString));
            }
            base.OnConfiguring(optionsBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<string>().HaveColumnType("varchar(200)");
            base.ConfigureConventions(configurationBuilder);
        }
    }
    #endregion
}
```

---

## Step 10 — Update `AppWebApi/Program.cs`

Replace the ~40 lines of inline DI wiring with the new extension methods. Also remove the `using Microsoft.EntityFrameworkCore;` import (no longer needed directly) and add the `Configuration.Extensions` and `DbContext.Extensions` namespaces.

The `#region` block changes from:

```csharp
#region Initializing the standard sw stack
var currentDir = Directory.GetCurrentDirectory();
var assembly = System.Reflection.Assembly.Load("Configuration");
builder.Configuration.SetBasePath(Path.Combine(currentDir, "../AppWebApi"))
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddUserSecrets(assembly);

builder.Services.Configure<AesEncryptionOptions>(...);
builder.Services.AddTransient<Encryptions>();
builder.Services.Configure<JwtOptions>(...);
builder.Services.Configure<DbConnectionSetsOptions>(...);
builder.Services.AddSingleton<DatabaseConnections>();
builder.Services.Configure<VersionOptions>(...);
builder.Services.AddSingleton<ILoggerProvider, InMemoryLoggerProvider>();
builder.Services.AddDbContext<MainDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("SqlServerDocker");
    options.UseSqlServer(connectionString, options => options.EnableRetryOnFailure());
});
#endregion
```

To:

```csharp
#region Initializing the standard sw stack using extensions
builder.Configuration.AddSecrets(builder.Environment);
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddVersionInfo();
builder.Services.AddInMemoryLogger();
builder.Services.AddUserBasedDbContext();
#endregion
```

The full `Program.cs` after the change:

```csharp
using Configuration;
using Configuration.Extensions;
using Configuration.Options;
using DbContext.Extensions;
using DbRepos;
using Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers().AddNewtonsoftJson(options =>
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
builder.Services.AddEndpointsApiExplorer();

#region Initializing the standard sw stack using extensions
builder.Configuration.AddSecrets(builder.Environment);
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddVersionInfo();
builder.Services.AddInMemoryLogger();
builder.Services.AddUserBasedDbContext();
#endregion

builder.Services.Configure<MySecretOptions>(
    options => builder.Configuration.GetSection(MySecretOptions.Position).Bind(options));

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Seido Music simple API",
#if DEBUG
        Version = "v2.0 DEBUG",
#else
        Version = "v2.0",
#endif
        Description = "This is an API used in Seido's various software developer training courses."
        + $"<br>DataSet: {builder.Configuration["DatabaseConnections:UseDataSetWithTag"]}"
        + $"<br>DefaultDataUser: {builder.Configuration["DatabaseConnections:DefaultDataUser"]}"
    });
});

builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<IAdminService, AdminServiceDb>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Seido Friends API v2.0");
});

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## Step 11 — Update `AppWebApi/appsettings.json`

Remove the `ConnectionStrings` section — the connection string is now assembled at runtime from `DatabaseConnections` options plus user secrets, so no connection string should be hardcoded here.

Also change `DefaultDataUser` from `"dbo"` to `"root"` (the seed operation in this branch creates albums, which requires broader permissions during development).

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information"
        },
        "Console": {
            "LogLevel": {
                "Services": "Information",
                "AppWebApi.Controllers": "None",
                "DbRepos": "None"
            }
        },
        "InMemory": {
            "LogLevel": {
                "Services": "Information",
                "AppWebApi.Controllers": "Information",
                "DbRepos": "Information"
            }
        }
    },
    "AllowedHosts": "*",
    "ApplicationSecrets": {
        "SecretStorage": "UserSecrets"
    },
    "DatabaseConnections": {
        "DefaultDataUser": "root",
        "MigrationUser": "root",
        "UseDataSetWithTag": "sql-music.sqlserver.docker"
    }
}
```

---

## Step 12 — Update `DbRepos/AdminDbRepos.cs`

After seeding music groups, attach 2–5 random albums to each one before calling `SaveChangesAsync`. EF Core's change tracker follows the navigation property and inserts the `Albums` rows with the correct foreign key automatically:

```csharp
var musicGroups = seeder.ItemsToList<MusicGroupDbM>(seedCount);

//Set between 2 and 5 albums for each music group
musicGroups.ForEach(mg => mg.AlbumsDbM = seeder.ItemsToList<AlbumDbM>(seeder.Next(2, 5)));

_dbContext.MusicGroups.AddRange(musicGroups);
await _dbContext.SaveChangesAsync();
```

---

## Step 13 — Delete files that are no longer needed

```bash
rm DbContext/appsettings.json         # replaced by EFC_AppSettingsFolder env var
```

---

## Step 14 — Regenerate EF Core migrations

The database schema has changed (new `supusr` schema, new `Albums` table), so existing migrations must be dropped and recreated. Use the updated rebuild script, which patches `appsettings.json` and sets `EFC_AppSettingsFolder` automatically:

```bash
cd _scripts

# SQL Server
./database-rebuild-all.sh sql-music sqlserver docker root ../AppWebApi

```

The script does the following for each run:
1. Patches `UseDataSetWithTag` and `DefaultDataUser` in `AppWebApi/appsettings.json`
2. Sets `EFC_AppSettingsFolder` to the resolved path of `AppWebApi/`
3. Drops the existing database (`dotnet ef database drop`)
4. Deletes `DbContext/Migrations/<DbContext>/`
5. Creates a fresh migration (`dotnet ef migrations add miInitial`)
6. Applies it to the database (`dotnet ef database update`)

After the migrations run, verify the result in Azure Data Studio:
- The `supusr` schema should exist with two tables: `MusicGroups` and `Albums`
- `Albums` should have a non-nullable foreign key column pointing to `MusicGroups`

Call endpoint `/api/admin/Seed` to populate both tables and confirm the relationship is working.
