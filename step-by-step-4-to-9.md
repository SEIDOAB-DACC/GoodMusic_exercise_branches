# Step-by-step: from `4-dependency-injection` to `9-dbcontext`

The goal of this branch is to introduce a **real database** using Entity Framework Core Code First, and to organise the code into a proper **layered architecture** of five projects.

Work through the steps in order — each project depends on the one before it.

---

## Step 1 — Create the `Models` project

This project holds pure C# domain types. It has no EF Core or web dependencies.

```bash
# run from the solution root
dotnet new classlib -n Models
```

**`Models/Models.csproj`** — add a reference to `Configuration` (for transitive NuGet access) and `NewtonsoftJson`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.NewtonsoftJson" Version="10.0.10" />
  </ItemGroup>
</Project>
```

**`Models/Interfaces.cs`** — the public contract:

```csharp
namespace Models.Interfaces;

public enum MusicGenre { Rock, Blues, Jazz, Metal }

public interface IMusicGroup
{
    public Guid MusicGroupId { get; set; }
    public string Name { get; set; }
}
```

**`Models/MusicGroup.cs`** — the domain class. Properties are `virtual` so `DbModels` can override them:

```csharp
using Seido.Utilities.SeedGenerator;
using Models.Interfaces;

namespace Models;

public class MusicGroup : IMusicGroup, ISeed<MusicGroup>
{
    public virtual Guid MusicGroupId { get; set; }
    public virtual string Name { get; set; }
    public virtual MusicGenre Genre { get; set; }

    public MusicGroup() {}
    public MusicGroup(MusicGroup org)
    {
        Seeded = org.Seeded;
        MusicGroupId = org.MusicGroupId;
        Name = org.Name;
        Genre = org.Genre;
    }

    public virtual bool Seeded { get; set; } = false;
    public virtual MusicGroup Seed(SeedGenerator seedGenerator)
    {
        Seeded = true;
        MusicGroupId = Guid.NewGuid();
        Name = seedGenerator.MusicGroupName;
        Genre = seedGenerator.FromEnum<MusicGenre>();
        return this;
    }
}
```

**`Models/SeedGenerator.cs`** — a large utility class (namespace `Seido.Utilities.SeedGenerator`) that generates randomised data from a JSON seed file. Copy this file directly from the branch:

```bash
git show 9-dbcontext:Models/SeedGenerator.cs > Models/SeedGenerator.cs
```

---

## Step 2 — Create the `DbModels` project

EF Core entity classes live here. Each class inherits from its `Models` counterpart and adds database annotations.

```bash
dotnet new classlib -n DbModels
```

**`DbModels/DbModels.csproj`**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
    <ProjectReference Include="..\Models\Models.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.10" />
    <PackageReference Include="EFCore.CheckConstraints" Version="10.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.NewtonsoftJson" Version="10.0.10" />
  </ItemGroup>
</Project>
```

**`DbModels/MusicGroupDbM.cs`** — inherits `MusicGroup` and marks the PK with `[Key]`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;

namespace DbModels;

public class MusicGroupDbM : MusicGroup, ISeed<MusicGroupDbM>
{
    [Key]
    public override Guid MusicGroupId { get; set; }

    public MusicGroupDbM() : base()
    {
        MusicGroupId = Guid.NewGuid();
    }

    public override MusicGroupDbM Seed(SeedGenerator seedGenerator)
    {
        base.Seed(seedGenerator);
        return this;
    }
}
```

Why `[Key]` here and not in `Models`? The `Models` project must not reference EF Core. Annotations belong in `DbModels`.

---

## Step 3 — Create the `DbContext` project

This project owns the EF Core `DbContext` and all Code First migrations.

```bash
dotnet new classlib -n DbContext
```

**`DbContext/DbContext.csproj`** — requires EF Core providers and migration tools:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
    <ProjectReference Include="..\DbModels\DbModels.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.10" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageReference Include="Microting.EntityFrameworkCore.MySql" Version="10.0.10" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.10" />
    <PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" Version="8.22.0" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.22.0" />
  </ItemGroup>
</Project>
```

**`DbContext/MainDbContext.cs`** — the base context plus three nested sub-contexts, one per supported database engine. The nested sub-contexts are used **only by the EF migration CLI tool**; the runtime uses `MainDbContext` directly via DI.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using DbModels;

namespace DbContext;

public class MainDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    #region C# model of database tables
    public DbSet<MusicGroupDbM> MusicGroups { get; set; }
    #endregion

    public MainDbContext() { }
    public MainDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    // Reads the connection string at design time (during migrations)
    // because the DI container is not available when running `dotnet ef` commands
    protected string GetConnectionString(string connectionStringName)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = config.GetConnectionString(connectionStringName);
        System.Console.WriteLine($"Design time connection string: {connectionString}");
        return connectionString;
    }

    #region Per-database sub-contexts used only for EF migrations

    public class SqlServerDbContext : MainDbContext
    {
        public SqlServerDbContext() { }
        public SqlServerDbContext(DbContextOptions options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var cs = GetConnectionString("SqlServerDocker");
                optionsBuilder.UseSqlServer(cs, o => o.EnableRetryOnFailure());
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
                var cs = GetConnectionString("MySqlDocker");
                optionsBuilder.UseMySql(cs, ServerVersion.AutoDetect(cs),
                    b => b.SchemaBehavior(
                        Microting.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Translate,
                        (schema, table) => $"{schema}_{table}"));
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
                var cs = GetConnectionString("PostgreSqlDocker");
                optionsBuilder.UseNpgsql(cs);
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

**`DbContext/appsettings.json`** — used exclusively by the `dotnet ef` migration tool (design time). The runtime web app reads its own `AppWebApi/appsettings.json`. Both files need the same `ConnectionStrings` section:

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information" }
  },
  "AllowedHosts": "*",
  "DatabaseConnections": {
    "DefaultDataUser": "root",
    "MigrationUser": "root",
    "UseDataSetWithTag": "sql-music.sqlserver.docker"
  },
  "ConnectionStrings": {
    "SqlServerDocker": "Data Source=localhost,14333;Initial Catalog=sql-music;Persist Security Info=True;User ID=sa;Pwd=<your-password>;Encrypt=False;"
  }
}
```

---

## Step 4 — Create the `DbRepos` project

The **repository layer** is the only layer permitted to access `DbContext` directly.

```bash
dotnet new classlib -n DbRepos
```

**`DbRepos/DbRepos.csproj`**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DbContext\DbContext.csproj" />
    <ProjectReference Include="..\DbModels\DbModels.csproj" />
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
  </ItemGroup>
</Project>
```

**`DbRepos/AdminDbRepos.cs`** — receives `MainDbContext` via constructor injection; never creates it with `new`:

```csharp
using Microsoft.Extensions.Logging;
using Seido.Utilities.SeedGenerator;
using DbModels;
using DbContext;
using Configuration;

namespace DbRepos;

public class AdminDbRepos
{
    private const string _seedSource = "./app-seeds.json";
    private readonly ILogger<AdminDbRepos> _logger;
    private Encryptions _encryptions;
    private readonly MainDbContext _dbContext;

    public async Task SeedAsync(int seedCount)
    {
        var seeder = new SeedGenerator(Path.GetFullPath(_seedSource));
        var musicGroups = seeder.ItemsToList<MusicGroupDbM>(seedCount);
        _dbContext.MusicGroups.AddRange(musicGroups);
        await _dbContext.SaveChangesAsync();
    }

    public AdminDbRepos(ILogger<AdminDbRepos> logger, Encryptions encryptions, MainDbContext context)
    {
        _logger = logger;
        _encryptions = encryptions;
        _dbContext = context;
    }
}
```

**`AppWebApi/app-seeds.json`** — the JSON seed data file read by `SeedGenerator`. Copy it from the branch:

```bash
git show 9-dbcontext:AppWebApi/app-seeds.json > AppWebApi/app-seeds.json
```

---

## Step 5 — Create the `Services` project

The **service layer** sits between controllers and repos. Controllers never reference `DbRepos` directly.

```bash
dotnet new classlib -n Services
```

**`Services/Services.csproj`**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
    <ProjectReference Include="..\DbRepos\DbRepos.csproj" />
    <ProjectReference Include="..\Models\Models.csproj" />
  </ItemGroup>
</Project>
```

**`Services/Interfaces.cs`** — the contract the controller depends on:

```csharp
namespace Services;

public interface IAdminService
{
    public Task SeedAsync(int seedCount);
}
```

**`Services/AdminServiceDb.cs`** — currently a thin pass-through; business logic goes here in later branches:

```csharp
using Microsoft.Extensions.Logging;
using DbRepos;

namespace Services;

public class AdminServiceDb : IAdminService
{
    private readonly AdminDbRepos _repo = null;
    private readonly ILogger<AdminServiceDb> _logger = null;

    public Task SeedAsync(int seedCount) => _repo.SeedAsync(seedCount);

    public AdminServiceDb(AdminDbRepos repo)
    {
        _repo = repo;
    }
    public AdminServiceDb(AdminDbRepos repo, ILogger<AdminServiceDb> logger) : this(repo)
    {
        _logger = logger;
    }
}
```

---

## Step 6 — Add `InMemoryLoggerProvider` to `Configuration`

Create **`Configuration/InMemoryLoggerProvider.cs`** — a custom `ILoggerProvider` that accumulates log messages in memory so they can be returned as JSON from the `Log` endpoint:

```csharp
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Configuration;

[ProviderAlias("InMemory")]
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly object _locker = new object();
    private readonly List<LogMessage> _messages = new List<LogMessage>();

    public Task<List<LogMessage>> MessagesAsync => Task.Run(() =>
    {
        lock (_locker)
        {
            return _messages?.Select(i => new LogMessage(i)).ToList();
        }
    });

    public List<LogMessage> Messages => _messages.ToList();

    void IDisposable.Dispose() { }

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(this, categoryName);

    private void Log<TState>(string categoryName, LogLevel logLevel, EventId eventId,
        TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = new LogMessage
        {
            Type = logLevel,
            Timestamp = DateTimeOffset.UtcNow,
            Message = formatter(state, exception) + (exception == null ? "" : "\r\n" + exception),
            Category = categoryName,
            EventId = eventId.Id,
        };
        lock (_locker) _messages.Add(message);
    }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly InMemoryLoggerProvider _provider;
        private readonly string _categoryName;

        public InMemoryLogger(InMemoryLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
            => _provider.Log(_categoryName, logLevel, eventId, state, exception, formatter);

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable BeginScope<TState>(TState state) => null;
    }
}

public sealed class LogMessage
{
    public LogLevel Type { get; set; }

    [JsonConverter(typeof(TimestampJsonConverter))]
    public DateTimeOffset Timestamp { get; set; }

    public string Message { get; set; }
    public string Category { get; set; }
    public int EventId { get; set; }

    public override string ToString() => $"{Category}: {Message}";

    public LogMessage() { }
    public LogMessage(LogMessage org)
    {
        Type = org.Type;
        Timestamp = org.Timestamp;
        Message = org.Message;
        Category = org.Category;
        EventId = org.EventId;
    }
}

public sealed class TimestampJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(DateTimeOffset);

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        => writer.WriteValue(((DateTimeOffset)value).ToUnixTimeMilliseconds());

    public override object ReadJson(JsonReader reader, Type objectType,
        object existingValue, JsonSerializer serializer)
        => DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(reader.Value));
}
```

---

## Step 7 — Delete the files that are no longer needed

```bash
rm Configuration/MusicGenreService.cs
rm AppWebApi/Controllers/MusicGenreController.cs
```

These files were teaching examples for DI in branch 4 and are replaced by the real service stack.

---

## Step 8 — Update `AppWebApi/appsettings.json`

Two changes:

1. Change `DefaultDataUser` from `"root"` to `"dbo"` — the app runs as a lower-privileged user; `root` is for migrations only.
2. Add a `ConnectionStrings` section — EF Core reads this via `GetConnectionString("SqlServerDocker")`:

```json
"DatabaseConnections": {
  "DefaultDataUser": "dbo",
  "MigrationUser": "root",
  "UseDataSetWithTag": "sql-music.sqlserver.docker"
},

"ConnectionStrings": {
  "SqlServerDocker": "Data Source=localhost,14333;Initial Catalog=sql-music;Persist Security Info=True;User ID=sa;Pwd=<your-password>;Encrypt=False;"
}
```

Also add the `InMemory` logger section under `Logging` to control which categories are captured in memory vs the console:

```json
"Logging": {
  "LogLevel": { "Default": "Information", "Microsoft": "Warning" },
  "Console": {
    "LogLevel": {
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
}
```

---

## Step 9 — Update `AppWebApi/AppWebApi.csproj`

Add project references to `Models` and `Services`:

```xml
<ItemGroup>
  <ProjectReference Include="..\Configuration\Configuration.csproj"/>
  <ProjectReference Include="..\Models\Models.csproj"/>
  <ProjectReference Include="..\Services\Services.csproj"/>
</ItemGroup>
```

---

## Step 10 — Update `AppWebApi/Program.cs`

**10a.** Add new `using` directives at the top:

```csharp
using Microsoft.EntityFrameworkCore;

using Configuration;
using Configuration.Options;
using DbContext;
using DbRepos;
using Services;
```

**10b.** Remove the `IMusicGenreService` registration (no longer exists):

```csharp
// DELETE these lines:
//builder.Services.AddTransient<IMusicGenreService, Classics>();
builder.Services.AddTransient<IMusicGenreService, Modern>();
```

**10c.** Register the custom logger — add this after the existing singleton/transient registrations:

```csharp
// Replaces the default console logger for this provider
builder.Services.AddSingleton<ILoggerProvider, InMemoryLoggerProvider>();
```

**10d.** Register `MainDbContext` — add inside the `#region` block, after `DatabaseConnections`:

```csharp
builder.Services.AddDbContext<MainDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("SqlServerDocker");
    options.UseSqlServer(connectionString, options => options.EnableRetryOnFailure());
});
```

**10e.** Register the repo and service — add after `SwaggerGen`, before `var app = builder.Build()`:

```csharp
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<IAdminService, AdminServiceDb>();
```

> Why `Scoped`? `MainDbContext` is scoped by default (one instance per HTTP request). `AdminDbRepos` and `AdminServiceDb` hold references to it, so they must match that lifetime.

---

## Step 11 — Update `AppWebApi/Controllers/AdminController.cs`

**11a.** Replace the `using Configuration;` line with:

```csharp
using Services;
using Configuration;
using Configuration.Options;
```

**11b.** Add the service field below the existing `_dbConnections` field:

```csharp
readonly Encryptions _encryptions = null;
readonly DatabaseConnections _dbConnections = null;
readonly IAdminService _service;   // ADD
```

**11c.** Add the `ConnectionString` endpoint (diagnostic — returns the raw connection string):

```csharp
//GET: api/admin/connectionstring
[HttpGet()]
[ActionName("ConnectionString")]
[ProducesResponseType(200, Type = typeof(string))]
public IActionResult ConnectionString()
{
    try
    {
        var connectionString = _configuration.GetConnectionString("SqlServerDocker");
        _logger.LogInformation($"{nameof(ConnectionString)}:\n{JsonConvert.SerializeObject(connectionString)}");
        return Ok(connectionString);
    }
    catch (Exception ex)
    {
        _logger.LogError($"{nameof(ConnectionString)}: {ex.Message}");
        return BadRequest(ex.Message);
    }
}
```

**11d.** Add the `Seed` endpoint (triggers database seeding through the service layer):

```csharp
//GET: api/admin/seed?seedCount={count}
[HttpGet()]
[ActionName("Seed")]
[ProducesResponseType(200, Type = typeof(string))]
[ProducesResponseType(400, Type = typeof(string))]
public async Task<IActionResult> Seed(int seedCount)
{
    try
    {
        _logger.LogInformation($"{nameof(Seed)}");
        await _service.SeedAsync(seedCount);
        return Ok($"Seeding {seedCount} items completed successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError($"{nameof(Seed)}: {ex.Message}");
        return BadRequest(ex.Message);
    }
}
```

**11e.** Add the `Log` endpoint. Note `[FromServices]` — this injects `ILoggerProvider` directly into the action parameter without adding it to the constructor, since only this one endpoint needs it:

```csharp
//GET: api/admin/log
[HttpGet()]
[ActionName("Log")]
[ProducesResponseType(200, Type = typeof(IEnumerable<LogMessage>))]
public async Task<IActionResult> Log([FromServices] ILoggerProvider _loggerProvider)
{
    if (_loggerProvider is InMemoryLoggerProvider cl)
        return Ok(await cl.MessagesAsync);

    return Ok("No messages in log");
}
```

**11f.** Extend the constructor signature to accept `IAdminService`:

```csharp
// before:
public AdminController(...,
    Encryptions encryptions, DatabaseConnections dbConnections)

// after:
public AdminController(...,
    Encryptions encryptions, DatabaseConnections dbConnections,
    IAdminService service)
```

And assign it in the body:

```csharp
_encryptions  = encryptions;
_dbConnections = dbConnections;
_service = service;   // ADD
```

---

## Step 12 — Register the new projects in `GoodMusic.slnx`

```xml
<Solution>
  <Project Path="Configuration/Configuration.csproj" />
  <Project Path="DbContext/DbContext.csproj" />
  <Project Path="DbModels/DbModels.csproj" />
  <Project Path="DbRepos/DbRepos.csproj" />
  <Project Path="Services/Services.csproj" />
  <Project Path="AppWebApi/AppWebApi.csproj" />
  <Project Path="Models/Models.csproj" />
</Solution>
```

---

## Step 13 — Create the EF migration and update the database

### Option A — use the rebuild script (recommended, follows `readme-clr1.txt` step 1)

Open a terminal in the `_scripts` folder and run the all-in-one script. It builds the solution, runs the migration, and updates the database in one step.

**macOS / Linux:**
```bash
cd _scripts
./database-rebuild-all.sh sql-music sqlserver docker root ../AppWebApi
```

**Windows:**
```powershell
cd _scripts
./database-rebuild-all.ps1 sql-music sqlserver docker root ../AppWebApi
```

Ensure there are no errors from the build, migration, or database update steps before continuing.

### Option B — run EF commands manually

Open a terminal **in the `DbContext` folder** (the `dotnet ef` tool reads `appsettings.json` from the current directory).

```bash
cd DbContext

# Create the initial migration
dotnet ef migrations add miInitial --context SqlServerDbContext --output-dir Migrations/SqlServerDbContext

# Apply the migration to the database
dotnet ef database update --context SqlServerDbContext
```

To drop and rebuild from scratch:
```bash
dotnet ef database drop -f --context SqlServerDbContext
dotnet ef database update --context SqlServerDbContext
```

---

## Summary

| Step | Action | What it introduces |
|------|--------|--------------------|
| 1 | Create `Models` | Domain types (`IMusicGroup`, `MusicGroup`, `SeedGenerator`) |
| 2 | Create `DbModels` | EF entity `MusicGroupDbM` with `[Key]` |
| 3 | Create `DbContext` | `MainDbContext`, DB-specific sub-contexts, design-time config |
| 4 | Create `DbRepos` | `AdminDbRepos.SeedAsync` — the only place touching `DbContext` |
| 5 | Create `Services` | `IAdminService` / `AdminServiceDb` — controller delegates to this |
| 6 | Add to `Configuration` | `InMemoryLoggerProvider` — collects log messages as JSON |
| 7 | Delete | `MusicGenreService.cs`, `MusicGenreController.cs` |
| 8 | Update `appsettings.json` | Add `ConnectionStrings`, logging config, change default user |
| 9 | Update `AppWebApi.csproj` | Add `Models` and `Services` references |
| 10 | Update `Program.cs` | Register `DbContext`, `AdminDbRepos`, `IAdminService`, logger |
| 11 | Update `AdminController` | Add `IAdminService`, `Seed`, `Log`, `ConnectionString` endpoints |
| 12 | Update `GoodMusic.slnx` | Add all five new projects |
| 13 | Run EF migration | Create database schema |
