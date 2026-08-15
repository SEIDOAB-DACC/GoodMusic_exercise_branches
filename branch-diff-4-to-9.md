# Code Differences: `4-dependency-injection` → `9-dbcontext`

## Overview

Branch `9-dbcontext` introduces a **layered architecture** with a real database. The project grows from a single `AppWebApi` + `Configuration` solution into five separate projects connected in a chain:

```
AppWebApi → Services → DbRepos → DbContext → DbModels → Models
                                           ↘ Configuration
```

Each layer has a single responsibility and depends only on the layer below it.

---

## Solution structure changes (`GoodMusic.slnx`)

Four new projects are added to the solution:

| New project | Purpose |
|---|---|
| `Models` | Domain model classes (no EF, no web, pure C#) |
| `DbModels` | EF Core database model classes (extends `Models`) |
| `DbContext` | `DbContext` class + Code First migration scaffolding |
| `DbRepos` | Repository layer — all direct `DbContext` access lives here |
| `Services` | Service layer — business logic, delegates to repos |

---

## New project: `Models`

Defines the **domain model** independently of the database.

**`Models/Interfaces.cs`** — the public contract:
```csharp
public enum MusicGenre { Rock, Blues, Jazz, Metal }
public interface IMusicGroup
{
    Guid MusicGroupId { get; set; }
    string Name { get; set; }
}
```

**`Models/MusicGroup.cs`** — the domain class:
- Implements `IMusicGroup` and `ISeed<MusicGroup>` (from `Seido.Utilities.SeedGenerator`)
- Properties are `virtual` so the database model layer can override them
- Contains a copy constructor and a `Seed(SeedGenerator)` method for generating random test data

**`Models/SeedGenerator.cs`** — provides randomised values for seeding (names, genres, etc.)

---

## New project: `DbModels`

Contains the **EF Core entity** classes. Each `DbM` class inherits from its `Models` counterpart and adds database-specific annotations.

**`DbModels/MusicGroupDbM.cs`**:
```csharp
public class MusicGroupDbM : MusicGroup, ISeed<MusicGroupDbM>
{
    [Key]
    public override Guid MusicGroupId { get; set; }  // tells EF Core this is the PK

    public MusicGroupDbM() : base()
    {
        MusicGroupId = Guid.NewGuid();  // EF Code First does not auto-generate GUIDs
    }
}
```

Separating `MusicGroup` (Models) from `MusicGroupDbM` (DbModels) means the web API and service layers work with the clean domain type, not with EF-annotated types.

---

## New project: `DbContext`

Contains the `MainDbContext` and all Code First migration files.

**`DbContext/MainDbContext.cs`**:
- Extends `Microsoft.EntityFrameworkCore.DbContext`
- Declares `DbSet<MusicGroupDbM> MusicGroups` — this is the mapping between C# and the database table
- Contains a `GetConnectionString` helper used at **design time** (EF migrations) that reads `appsettings.json` directly via `ConfigurationBuilder`, since the DI container is not running during `dotnet ef` commands
- Nested inner classes `SqlServerDbContext`, `MySqlDbContext`, `PostgresDbContext` each override `OnConfiguring` to supply their respective connection string — these are used **only for migrations**, not at runtime

**`DbContext/appsettings.json`** — a separate config file used exclusively by the EF migration tooling (design time). It contains the connection strings so `dotnet ef migrations add` and `dotnet ef database update` can run without the full web host.

**`DbContext/EFC migration commands.txt`** — documents the exact `dotnet ef` commands needed to create migrations and update the database for each supported server type.

---

## New project: `DbRepos`

The **repository layer** — the only place in the solution that directly touches `DbContext`.

**`DbRepos/AdminDbRepos.cs`**:
```csharp
public class AdminDbRepos
{
    private readonly MainDbContext _dbContext;
    private Encryptions _encryptions;

    public async Task SeedAsync(int seedCount)
    {
        var seeder = new SeedGenerator(Path.GetFullPath("./app-seeds.json"));
        var musicGroups = seeder.ItemsToList<MusicGroupDbM>(seedCount);
        _dbContext.MusicGroups.AddRange(musicGroups);
        await _dbContext.SaveChangesAsync();
    }

    public AdminDbRepos(ILogger<AdminDbRepos> logger, Encryptions encryptions, MainDbContext context)
    { ... }
}
```

Receives `MainDbContext` via constructor injection (not created with `new`). All `await`/`async` database calls are in this layer.

---

## New project: `Services`

The **service layer** sits between the API controllers and the repositories.

**`Services/Interfaces.cs`**:
```csharp
public interface IAdminService
{
    Task SeedAsync(int seedCount);
}
```

**`Services/AdminServiceDb.cs`**:
```csharp
public class AdminServiceDb : IAdminService
{
    private readonly AdminDbRepos _repo;

    public Task SeedAsync(int seedCount) => _repo.SeedAsync(seedCount);

    public AdminServiceDb(AdminDbRepos repo) { _repo = repo; }
}
```

Currently a thin pass-through, but this layer is where business logic (validation, transformation, transactions across multiple repos) would go in later branches.

---

## Removed files

| File | Reason |
|---|---|
| `Configuration/MusicGenreService.cs` | Was only a DI teaching example; replaced by real services |
| `AppWebApi/Controllers/MusicGenreController.cs` | Removed with the above |

---

## Modified: `Configuration/InMemoryLoggerProvider.cs` (new file)

A custom `ILoggerProvider` / `ILogger` implementation that stores log messages in a thread-safe in-memory list instead of writing to the console. This allows the `Log` endpoint to return the application's own log messages as a JSON response.

Key design points:
- `InMemoryLoggerProvider` implements `ILoggerProvider` — registered once as a **singleton**
- `InMemoryLogger` (private inner class) implements `ILogger` — created per category by `CreateLogger(categoryName)`
- `LogMessage` is a plain data class with a `TimestampJsonConverter` that serialises `DateTimeOffset` to a Unix timestamp
- Thread safety is handled with a `lock` on a private `_locker` object

---

## Modified: `AppWebApi/appsettings.json`

Two changes:
1. `DefaultDataUser` changed from `"root"` to `"dbo"` — the application now runs as a less-privileged user; `root` is reserved for migrations only.
2. A `ConnectionStrings` section is added:
```json
"ConnectionStrings": {
  "SqlServerDocker": "Data Source=localhost,14333;Initial Catalog=sql-music;..."
}
```
The connection string is read at runtime by `Program.cs` and passed to `AddDbContext`. The `DatabaseConnections` class still exists (for setup info), but the actual EF connection string now comes from the standard `ConnectionStrings` section.

---

## Modified: `AppWebApi/Program.cs`

**New `using` directives:**
```csharp
using Microsoft.EntityFrameworkCore;
using DbContext;
using DbRepos;
using Services;
```

**Removed registration:**
```csharp
// Gone — MusicGenreService and IMusicGenreService no longer exist
builder.Services.AddTransient<IMusicGenreService, Modern>();
```

**New: custom logger registration** (replaces the default console logger for this provider):
```csharp
builder.Services.AddSingleton<ILoggerProvider, InMemoryLoggerProvider>();
```

**New: DbContext registration** (scoped by default — one instance per HTTP request):
```csharp
builder.Services.AddDbContext<MainDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("SqlServerDocker");
    options.UseSqlServer(connectionString, options => options.EnableRetryOnFailure());
});
```

**New: repo and service registrations** (scoped — tied to the request lifetime, same as `DbContext`):
```csharp
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<IAdminService, AdminServiceDb>();
```

---

## Modified: `AppWebApi/Controllers/AdminController.cs`

**New field and constructor parameter:**
```csharp
readonly IAdminService _service;

// constructor:
public AdminController(..., IAdminService service)
{
    ...
    _service = service;
}
```

**New endpoint — `ConnectionString`** (diagnostic, returns the raw connection string):
```csharp
[HttpGet(), ActionName("ConnectionString")]
public IActionResult ConnectionString()
{
    var connectionString = _configuration.GetConnectionString("SqlServerDocker");
    return Ok(connectionString);
}
```

**New endpoint — `Seed`** (triggers database seeding via the service layer):
```csharp
[HttpGet(), ActionName("Seed")]
public async Task<IActionResult> Seed(int seedCount)
{
    await _service.SeedAsync(seedCount);
    return Ok($"Seeding {seedCount} items completed successfully");
}
```

**New endpoint — `Log`** (returns in-memory log entries; uses `[FromServices]` for one-off injection):
```csharp
[HttpGet(), ActionName("Log")]
public async Task<IActionResult> Log([FromServices] ILoggerProvider _loggerProvider)
{
    if (_loggerProvider is InMemoryLoggerProvider cl)
        return Ok(await cl.MessagesAsync);
    return Ok("No messages in log");
}
```

`[FromServices]` injects `ILoggerProvider` directly into this action parameter without adding it to the constructor — useful for dependencies needed in only one endpoint.

---

## New seed data: `AppWebApi/app-seeds.json`

A JSON file containing lists of names, quotes, addresses, and music group/album name fragments used by `SeedGenerator` to build randomised but realistic-looking test records.

---

## Lifetime summary

| Service | Lifetime | Reason |
|---|---|---|
| `InMemoryLoggerProvider` | `Singleton` | Must accumulate messages across all requests |
| `DatabaseConnections` | `Singleton` | Reads config once; result is immutable |
| `Encryptions` | `Transient` | Stateless; cheap to create |
| `MainDbContext` | `Scoped` (default for `AddDbContext`) | EF Core DbContext must not be shared across requests |
| `AdminDbRepos` | `Scoped` | Holds a reference to `DbContext`; must match its lifetime |
| `IAdminService` / `AdminServiceDb` | `Scoped` | Holds a reference to `AdminDbRepos`; must match its lifetime |
