# Code differences: `9-dbcontext` → `13-schema-annotations`

This branch builds on the layered architecture introduced in `9-dbcontext` and adds two major themes:
1. **Schema annotations** on the database models — explicit control over table names, schemas, keys, and relationships.
2. **Refactored startup code** — all the inline `Program.cs` wiring is extracted into extension methods.

---

## 1. New domain model: `Album`

### `Models/Interfaces.cs`
A new `IAlbum` interface is added alongside the existing `IMusicGroup`. `IMusicGroup` gains an `Albums` navigation property:

```csharp
// Before
public interface IMusicGroup
{
    public Guid MusicGroupId { get; set; }
    public string Name { get; set; }
}

// After
public interface IMusicGroup
{
    public Guid MusicGroupId { get; set; }
    public string Name { get; set; }
    public List<IAlbum> Albums { get; set; }   // ← new
}

public interface IAlbum                         // ← new
{
    public Guid AlbumId { get; set; }
    public string Name { get; set; }
    public IMusicGroup MusicGroup { get; set; }
}
```

### `Models/Album.cs` *(new file)*
A plain C# model class that implements `IAlbum` — no EF Core attributes here, just domain data and seed logic:

```csharp
public class Album : IAlbum, ISeed<Album>
{
    public virtual Guid AlbumId { get; set; }
    public virtual string Name { get; set; }
    public virtual IMusicGroup MusicGroup { get; set; } = null;
    // ...seed logic...
}
```

### `Models/MusicGroup.cs`
The existing model gains the `Albums` navigation property required by the updated interface:

```csharp
public virtual List<IAlbum> Albums { get; set; } = new List<IAlbum>();
```

---

## 2. Schema annotations on database models

This is the central topic of the branch. EF Core data annotations are placed on the `DbM` classes (not on the domain models), giving precise control over how EF Core maps C# classes to database tables.

### `DbModels/MusicGroupDbM.cs`

```csharp
// Before
public class MusicGroupDbM : MusicGroup, ISeed<MusicGroupDbM>
{
    [Key]
    public override Guid MusicGroupId { get; set; }
    // ...
}

// After
[Table("MusicGroups", Schema = "supusr")]          // ← explicit table name + schema
public class MusicGroupDbM : MusicGroup, ISeed<MusicGroupDbM>
{
    [Key]
    public override Guid MusicGroupId { get; set; }

    // Navigation property pattern for interface-based relationships:
    [NotMapped]
    public override List<IAlbum> Albums { get => AlbumsDbM?.ToList<IAlbum>(); set => new NotImplementedException(); }

    [JsonIgnore]
    public virtual List<AlbumDbM> AlbumsDbM { get; set; } = null;
}
```

Key points:
- `[Table("MusicGroups", Schema = "supusr")]` — EF Core will create/expect the table in the `supusr` schema rather than `dbo`.
- The interface property `Albums` (typed `List<IAlbum>`) is decorated with `[NotMapped]` because EF Core cannot map interface types. A concrete sibling property `AlbumsDbM` (typed `List<AlbumDbM>`) is the actual EF Core navigation property.
- `[JsonIgnore]` on `AlbumsDbM` prevents infinite loops during JSON serialisation when the relationship is loaded.

### `DbModels/AlbumDbM.cs` *(new file)*

```csharp
[Table("Albums", Schema = "supusr")]
public class AlbumDbM : Album, ISeed<AlbumDbM>
{
    [Key]
    public override Guid AlbumId { get; set; }

    [Required]
    public override string Name { get; set; }

    // Interface property — not mapped:
    [NotMapped]
    public override IMusicGroup MusicGroup { get => MusicGroupDbM; set => new NotImplementedException(); }

    // Concrete EF Core navigation property (foreign key inferred by convention):
    [JsonIgnore]
    [Required]
    public virtual MusicGroupDbM MusicGroupDbM { get; set; } = null;
}
```

The `[NotMapped]` + concrete-sibling pattern is repeated here: the interface-typed property is exposed to the rest of the application, while EF Core uses the concrete-typed navigation property to build the relationship and foreign key.

---

## 3. `DbContext/MainDbContext.cs`

### New `DbSet` and debug helper

```csharp
public DbSet<AlbumDbM> Albums { get; set; }    // ← registers Albums table with EF Core

#if DEBUG
// Exposes the active connection string without the password — useful in debug endpoints
public string dbConnection => Regex.Replace(
    this.Database.GetConnectionString() ?? "", @"(pwd|password)=[^;]*;?", "",
    RegexOptions.IgnoreCase);
#endif
```

### `GetConnectionString` removed from `MainDbContext`

In `9-dbcontext`, each nested `DbContext` subclass called a private `GetConnectionString()` method that manually read `appsettings.json`. This is replaced by the new `ConfigureForDesignTime` extension (see below):

```csharp
// Before (in each nested DbContext subclass)
if (!optionsBuilder.IsConfigured)
{
    var connectionString = GetConnectionString("SqlServerDocker");
    optionsBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure());
}

// After
if (!optionsBuilder.IsConfigured)
{
    optionsBuilder = optionsBuilder.ConfigureForDesignTime(
        (options, connectionString) => options.UseSqlServer(connectionString, options => options.EnableRetryOnFailure()));
}
```

---

## 4. New extension methods

All the inline bootstrapping code is moved to dedicated extension methods, making each concern independently testable and reusable at design time.

### `Configuration/Extensions/` *(five new files)*

| File | Extension method | What it registers |
|---|---|---|
| `SecretsExtensions.cs` | `AddSecrets(env, folder?)` | `appsettings.json` + user secrets (dev) or Azure Key Vault (prod) |
| `DatabaseExtensions.cs` | `AddDatabaseConnections(config)` | `DbConnectionSetsOptions` + `DatabaseConnections` singleton |
| `EncryptionExtensions.cs` | `AddEncryptions(config)` | `AesEncryptionOptions` + `Encryptions` transient |
| `LoggerExtensions.cs` | `AddInMemoryLogger()` | `InMemoryLoggerProvider` singleton |
| `VersionExtensions.cs` | `AddVersionInfo()` | `VersionOptions` from assembly |

### `DbContext/Extensions/DbContextExtensions.cs` *(new file)*

`AddUserBasedDbContext()` replaces the verbose inline `AddDbContext` block in `Program.cs`. It reads `DefaultDataUser` from config, calls `DatabaseConnections.GetDataConnectionDetails()` to resolve the connection string, and selects the correct EF Core provider at runtime:

```csharp
public static IServiceCollection AddUserBasedDbContext(this IServiceCollection serviceCollection)
{
    serviceCollection.AddDbContext<MainDbContext>((serviceProvider, options) =>
    {
        var databaseConnections = serviceProvider.GetRequiredService<DatabaseConnections>();
        var conn = databaseConnections.GetDataConnectionDetails(userRole);

        if (/* SQLServer */) options.UseSqlServer(...);
        else if (/* MySql */) options.UseMySql(...);
        else if (/* PostgreSql */) options.UseNpgsql(...);
        else throw new InvalidDataException(...);
    });
    return serviceCollection;
}
```

### `DbContext/Extensions/DbContextDesignTimeExtensions.cs` *(new file)*

`ConfigureForDesignTime()` is what the nested DbContext subclasses call during `dotnet ef migrations`. When EF Core runs at design time `Program.cs` does not execute, so the full DI container does not exist. This extension recreates only the services needed to resolve a connection string:

```csharp
public static DbContextOptionsBuilder ConfigureForDesignTime(
    this DbContextOptionsBuilder optionsBuilder,
    Func<DbContextOptionsBuilder, string, DbContextOptionsBuilder> databaseOptions)
{
    var (configuration, databaseConnections) = CreateDesignTimeServices();
    var connection = GetDatabaseConnection(configuration, databaseConnections);
    return databaseOptions(optionsBuilder, connection.DbConnectionString);
}
```

It reads `EFC_AppSettingsFolder` from the environment to locate `appsettings.json`, then reuses the same `AddSecrets` and `AddDatabaseConnections` extensions used at runtime — one code path for both contexts.

---

## 5. `AppWebApi/Program.cs` — startup simplified

The ~40-line inline DI registration block is replaced by five one-liner calls:

```csharp
// Before (9-dbcontext) — inline in Program.cs
var assembly = System.Reflection.Assembly.Load("Configuration");
builder.Configuration.SetBasePath(...).AddJsonFile(...).AddUserSecrets(assembly);
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

// After (13-schema-annotations) — via extension methods
builder.Configuration.AddSecrets(builder.Environment);
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddVersionInfo();
builder.Services.AddInMemoryLogger();
builder.Services.AddUserBasedDbContext();
```

---

## 6. `AppWebApi/appsettings.json` — `ConnectionStrings` section removed

In `9-dbcontext` the connection string was hardcoded directly in `appsettings.json`:

```json
"ConnectionStrings": {
  "SqlServerDocker": "Data Source=localhost,14333;..."
}
```

In `13-schema-annotations` this section is gone. The connection string is now assembled at runtime by `DatabaseConnections.GetDataConnectionDetails()` from the structured `DatabaseConnections` configuration section plus user secrets — keeping credentials out of the settings file.

`DefaultDataUser` is also changed from `"dbo"` back to `"root"` in this branch, because the new seeding behaviour (adding albums) requires broader database permissions during development.

---

## 7. `AdminController.cs` — `ConnectionString` endpoint removed

The debug endpoint `GET api/admin/ConnectionString` that returned the raw connection string is deleted. Its purpose is superseded by the `#if DEBUG` `dbConnection` property on `MainDbContext`, which strips the password before exposing the value.

---

## 8. Seeding updated in `DbRepos/AdminDbRepos.cs`

Albums are now seeded together with music groups:

```csharp
// After seeding music groups, attach 2–5 random albums to each
musicGroups.ForEach(mg => mg.AlbumsDbM = seeder.ItemsToList<AlbumDbM>(seeder.Next(2, 5)));
_dbContext.MusicGroups.AddRange(musicGroups);
```

Because `AlbumDbM` has a `[Required]` foreign key to `MusicGroupDbM`, EF Core's change tracker automatically inserts the albums in the correct order and sets the foreign keys.

---

## 9. Database rebuild scripts updated

Both `_scripts/database-rebuild-all.sh` and `_scripts/database-rebuild-all.ps1` gain three new required parameters and take over responsibility for patching `appsettings.json` before running migrations.

### New parameters

| Position | Name | Values |
|---|---|---|
| 3 | `DeploymentTarget` | `docker` \| `azure` |
| 4 | `DefaultDataUser` | `root` \| `dbo` \| `supusr` \| `usr` \| `gstusr` |
| 5 | `AppSettingsFolder` | path to the folder containing `appsettings.json` |

Example invocations:
```bash
# bash
./database-rebuild-all.sh sql-music sqlserver docker dbo ../AppWebApi

# PowerShell
./database-rebuild-all.ps1 sql-music sqlserver docker dbo ../AppWebApi
```

### What the scripts now do before running `dotnet ef`

**1. Patch `appsettings.json` in place**

The scripts use `sed` (bash) / `-replace` (PowerShell) to update two values in the target project's `appsettings.json`:

```bash
# Sets e.g. "sql-music.sqlserver.docker"
sed -i '' 's/"UseDataSetWithTag":.*/"UseDataSetWithTag": "'$1'.'$2'.'$3'"/g' $AppSettingsFolder/appsettings.json

# Sets e.g. "dbo"
sed -i '' 's/"DefaultDataUser":.*/"DefaultDataUser": "'$4'"/g' $AppSettingsFolder/appsettings.json
```

This replaces the previous approach of hardcoding `UseDataSetWithTag` and `DefaultDataUser` in `appsettings.json` and makes the scripts database- and user-agnostic.

**2. Set `EFC_AppSettingsFolder` environment variable**

The new `DbContextDesignTimeExtensions.ConfigureForDesignTime()` reads this variable to find `appsettings.json` at design time. The scripts export it before every `dotnet ef` call:

```bash
export EFC_AppSettingsFolder="$AppSettingsFolder"
dotnet ef database drop  -f -c $DBContext -p ../DbContext -s ../DbContext
# ...
export EFC_AppSettingsFolder="$AppSettingsFolder"
dotnet ef migrations add miInitial ...
# ...
export EFC_AppSettingsFolder="$AppSettingsFolder"
dotnet ef database update ...
```

**3. Skip `database drop` for non-Docker targets**

The drop step is now conditional — it only runs when `DeploymentTarget == "docker"`. For an Azure target the database is managed externally and should not be dropped by the script:

```bash
if [[ $3 == "docker" ]]; then
    export EFC_AppSettingsFolder="$AppSettingsFolder"
    dotnet ef database drop -f -c $DBContext -p ../DbContext -s ../DbContext
fi
```

**4. Reminder comment added**

Both scripts end with a comment pointing to the SQL initialisation scripts that must be run separately to create schemas and users:

```bash
#to initialize the database you need to run the sql scripts
#../DbContext/SqlScripts/<db_type>/initDatabase.sql
```

---

## 10. EF Core migrations regenerated

New migrations are added for all three providers (SQL Server, MySQL, PostgreSQL). The SQL Server migration is replaced because the schema changed:

- Tables are now in the `supusr` schema instead of `dbo`.
- A new `Albums` table is created with a foreign key to `MusicGroups`.

The MySQL and PostgreSQL migrations are added for the first time in this branch.

---

## Summary of key concepts introduced

| Concept | Where |
|---|---|
| `[Table(name, Schema)]` — map a class to a specific schema | `MusicGroupDbM`, `AlbumDbM` |
| `[Key]`, `[Required]`, `[NotMapped]` data annotations | `MusicGroupDbM`, `AlbumDbM` |
| `[NotMapped]` + concrete sibling pattern for interface navigation properties | `MusicGroupDbM.Albums` / `AlbumsDbM`, `AlbumDbM.MusicGroup` / `MusicGroupDbM` |
| `[JsonIgnore]` to break serialisation cycles on navigation properties | `AlbumsDbM`, `MusicGroupDbM` in `AlbumDbM` |
| Extension methods for DI registration (one method per concern) | `Configuration/Extensions/`, `DbContext/Extensions/` |
| Shared configuration bootstrap for runtime and EF design-time | `SecretsExtensions`, `DbContextDesignTimeExtensions` |
| Script-driven `appsettings.json` patching + `EFC_AppSettingsFolder` env var | `database-rebuild-all.sh` / `.ps1` |
