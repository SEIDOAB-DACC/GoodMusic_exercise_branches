# Code differences: `18-crud-complete` → `20-database-objects`

This branch introduces proper **database-side objects** — a SQL view and a stored procedure — and wires them into the C# stack. It also adds input validation on write operations and a read-only `GuestController` that exposes only what the least-privileged database user can see.

---

## 1. SQL scripts — `DbContext/SqlScripts/sqlserver/`

Two new SQL scripts are added. They are run manually after the EF Core migration creates the tables; EF Core does not manage views or stored procedures by default.

### `initDatabase.sql`

Creates all database objects that live outside EF Core migrations:

**Schemas**

```sql
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'gstusr')
    EXEC('CREATE SCHEMA gstusr');
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'usr')
    EXEC('CREATE SCHEMA usr');
```

Two additional schemas are created alongside `supusr` (which EF Core already uses for tables). `gstusr` is the guest-user schema — it can only see aggregated counts, not raw data. `usr` is reserved for a regular user role added in a later branch.

**View — `gstusr.vwInfoDb`**

```sql
CREATE OR ALTER VIEW gstusr.vwInfoDb AS
    SELECT
        (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 1) as NrSeededMusicGroups,
        (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 0) as NrUnseededMusicGroups,
        (SELECT COUNT(*) FROM supusr.Albums WHERE Seeded = 1)      as NrSeededAlbums,
        (SELECT COUNT(*) FROM supusr.Albums WHERE Seeded = 0)      as NrUnseededAlbums;
```

Returns a single row of aggregate counts. Because it lives in the `gstusr` schema, access can be restricted to the guest database user without granting any access to the underlying tables.

**Stored procedure — `supusr.spDeleteAll`**

```sql
CREATE OR ALTER PROC supusr.spDeleteAll
    @seededParam       BIT = 1,
    @nrMusicGroupsAffected INT OUTPUT,
    @nrAlbumsAffected      INT OUTPUT
AS
    SET NOCOUNT ON;
    SELECT @nrMusicGroupsAffected = COUNT(*) FROM supusr.MusicGroups WHERE Seeded = @seededParam;
    SELECT @nrAlbumsAffected      = COUNT(*) FROM supusr.Albums      WHERE Seeded = @seededParam;

    DELETE FROM supusr.MusicGroups WHERE Seeded = @seededParam;
    DELETE FROM supusr.Albums      WHERE Seeded = @seededParam;

    SELECT * FROM gstusr.vwInfoDb;
```

Deletes all rows where `Seeded` matches the parameter (default: delete seeded rows) and then returns the current view state as a result set so the caller sees the counts after deletion in a single round-trip. Output parameters return the number of affected rows.

### `clearDatabase.sql`

Drops all the objects created by `initDatabase.sql` in the correct order — procedures first, then views, then tables (FK order), then the EF migrations history:

```sql
DROP PROCEDURE IF EXISTS supusr.spDeleteAll
DROP VIEW      IF EXISTS gstusr.vwInfoDb
DROP TABLE     IF EXISTS supusr.ArtistDbMMusicGroupDbM
DROP TABLE     IF EXISTS supusr.Albums
DROP TABLE     IF EXISTS supusr.MusicGroups
DROP TABLE     IF EXISTS __EFMigrationsHistory
```

This script replaces the need to drop the entire database when resetting — useful when the database server is shared or when schemas and users need to be preserved.

---

## 2. Map the view in `DbContext/MainDbContext.cs`

EF Core can query a view through a `DbSet` with no primary key. Two additions are made:

```csharp
// DbSet — exposes the view as a queryable set
public DbSet<GstUsrInfoDbDto> InfoDbView { get; set; }

// OnModelCreating — tells EF Core this maps to a view, not a table
modelBuilder.Entity<GstUsrInfoDbDto>().ToView("vwInfoDb", "gstusr").HasNoKey();
```

`HasNoKey()` is required because views do not have a primary key. EF Core will never track instances of `GstUsrInfoDbDto` or try to insert/update/delete them.

---

## 3. New DTOs — `Models/DTO/GstUsrDto.cs`

```csharp
public class GstUsrInfoDbDto
{
    public int NrSeededMusicGroups   { get; set; } = 0;
    public int NrUnseededMusicGroups { get; set; } = 0;
    public int NrSeededAlbums        { get; set; } = 0;
    public int NrUnseededAlbums      { get; set; } = 0;
}

public class GstUsrInfoAllDto
{
    public GstUsrInfoDbDto Db { get; set; } = null;
}
```

`GstUsrInfoDbDto` matches the column names returned by `vwInfoDb` — EF Core maps them by convention. `GstUsrInfoAllDto` is a wrapper that could be extended with data from other sources (e.g. in-memory stats) without changing the API shape.

---

## 4. `EnsureValidity()` added to CU DTOs — `Models/DTO/CuDto.cs`

Both `MusicGroupCUdto` and `AlbumCUdto` gain a validation method. It is the DTO's own responsibility to declare what constitutes a valid state:

### `MusicGroupCUdto.EnsureValidity()`

```csharp
public void EnsureValidity()
{
    if (!string.IsNullOrEmpty(Name) && !Regex.IsMatch(Name, @"^[a-zA-Z0-9\s\-\.!]*$"))
        throw new ArgumentException("Name can only contain letters (a-z), numbers (0-9), spaces, -, ., and !.");
    if (EstablishedYear <= 0)
        throw new ArgumentException("EstablishedYear has to be larger than zero");
    if (!Enum.IsDefined(typeof(MusicGenre), Genre))
        throw new ArgumentException("Genre has to be set to a valid value");
}
```

### `AlbumCUdto.EnsureValidity()`

```csharp
public void EnsureValidity()
{
    if (!string.IsNullOrEmpty(Name) && !Regex.IsMatch(Name, @"^[a-zA-Z0-9\s\-\.!]*$"))
        throw new ArgumentException("Name can only contain letters (a-z), numbers (0-9), spaces, -, ., and !.");
}
```

`EnsureValidity()` is called at the start of `UpdateItem` and `CreateItem` in both `MusicGroupsController` and `AlbumsController` — before any parsing or database access — so malformed input is rejected immediately with a clear `400 Bad Request`.

---

## 5. `DbRepos/AdminDbRepos.cs` — two new methods

### `RemoveSeedAsync` — calls the stored procedure via ADO.NET

EF Core has no built-in support for stored procedures with output parameters, so this method drops down to raw ADO.NET using the connection EF Core already manages:

```csharp
public async Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded)
{
    var connection = _dbContext.Database.GetDbConnection();
    using var command = connection.CreateCommand();
    command.CommandType = CommandType.StoredProcedure;
    command.CommandText = "supusr.spDeleteAll";

    List<DbParameter> parameters = new List<DbParameter>
    {
        new SqlParameter("seededParam",            seeded),
        new SqlParameter("nrMusicGroupsAffected",  SqlDbType.Int) { Direction = ParameterDirection.Output },
        new SqlParameter("nrAlbumsAffected",       SqlDbType.Int) { Direction = ParameterDirection.Output }
    };
    command.Parameters.AddRange(parameters.ToArray());

    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    using var reader = await command.ExecuteReaderAsync();

    // read the result set returned by SELECT * FROM gstusr.vwInfoDb inside the procedure
    if (reader.HasRows)
    {
        await reader.ReadAsync();
        var result_set = new GstUsrInfoDbDto
        {
            NrSeededMusicGroups   = Convert.ToInt32(reader["NrSeededMusicGroups"]),
            NrUnseededMusicGroups = Convert.ToInt32(reader["NrUnseededMusicGroups"]),
            NrSeededAlbums        = Convert.ToInt32(reader["NrSeededAlbums"]),
            NrUnseededAlbums      = Convert.ToInt32(reader["NrUnseededAlbums"])
        };
        await reader.CloseAsync();
    }

    // return the current view state via the EF Core DbSet path
    return await DbInfo();
}
```

Key points:
- `_dbContext.Database.GetDbConnection()` reuses EF Core's connection instead of opening a second one.
- The stored procedure's inline `SELECT * FROM gstusr.vwInfoDb` result set is read via `ExecuteReaderAsync` — the output parameters are available on the parameter objects after the reader is closed.
- The method ultimately returns `DbInfo()` (the EF Core path) rather than the reader result, keeping the response consistent with `InfoAsync`.

### `InfoAsync` and `DbInfo` — queries the view via EF Core

```csharp
public async Task<ResponseItemDto<GstUsrInfoAllDto>> InfoAsync() => await DbInfo();

private async Task<ResponseItemDto<GstUsrInfoAllDto>> DbInfo()
{
    var info = new GstUsrInfoAllDto();
    info.Db = await _dbContext.InfoDbView.FirstAsync();
    return new ResponseItemDto<GstUsrInfoAllDto>
    {
#if DEBUG
        ConnectionString = _dbContext.dbConnection,
#endif
        Item = info
    };
}
```

`_dbContext.InfoDbView.FirstAsync()` issues a `SELECT TOP 1 * FROM gstusr.vwInfoDb` — since the view always returns exactly one row, `FirstAsync` is appropriate.

---

## 6. `Services/Interfaces.cs` and `Services/AdminServiceDb.cs`

Two methods are added to `IAdminService` and delegated straight through in `AdminServiceDb`:

```csharp
// Interfaces.cs
public interface IAdminService
{
    public Task SeedAsync(int seedCount);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> GuestInfoAsync();
}

// AdminServiceDb.cs
public Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded) => _repo.RemoveSeedAsync(seeded);
public Task<ResponseItemDto<GstUsrInfoAllDto>> GuestInfoAsync()             => _repo.InfoAsync();
```

---

## 7. `AppWebApi/Controllers/AdminController.cs` — new `RemoveSeed` endpoint

```csharp
[HttpGet()]
[ActionName("RemoveSeed")]
[ProducesResponseType(200, Type = typeof(GstUsrInfoAllDto))]
[ProducesResponseType(400, Type = typeof(string))]
public async Task<IActionResult> RemoveSeed(string seeded = "true")
{
    try
    {
        bool seededArg = bool.Parse(seeded);
        _logger.LogInformation($"{nameof(RemoveSeed)}: seeded={seededArg}");
        var info = await _service.RemoveSeedAsync(seededArg);
        return Ok(info);
    }
    catch (Exception ex)
    {
        _logger.LogError($"{nameof(RemoveSeed)}: {ex.Message}");
        return BadRequest(ex.Message);
    }
}
```

This replaces the old workflow of rerunning the database rebuild script to clear seeded data. `seeded=true` (default) removes all seeded rows; `seeded=false` would remove user-created rows.

---

## 8. New `AppWebApi/Controllers/GuestController.cs`

A new controller with a single read-only endpoint. It is designed to be accessible with the lowest-privilege database user (`gstusr`) because it only reads from the view:

```csharp
[ApiController]
[Route("api/[controller]/[action]")]
public class GuestController : Controller
{
    readonly IAdminService _service;

    [HttpGet()]
    [ActionName("Info")]
    [ProducesResponseType(200, Type = typeof(GstUsrInfoAllDto))]
    public async Task<IActionResult> Info()
    {
        try
        {
            var info = await _service.GuestInfoAsync();
            _logger.LogInformation($"{nameof(Info)}:\n{JsonConvert.SerializeObject(info)}");
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{nameof(Info)}: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }
}
```

It reuses `IAdminService` rather than introducing a new service interface — the existing `GuestInfoAsync` method is the only operation needed here.

---

## 9. `EnsureValidity()` called in `MusicGroupsController` and `AlbumsController`

Both `UpdateItem` and `CreateItem` in both controllers gain a single line at the top of their `try` block:

```csharp
// Before (18-crud-complete)
public async Task<IActionResult> UpdateItem(string id, [FromBody] MusicGroupCUdto item)
{
    try
    {
        var idArg = Guid.Parse(id);
        ...
    }
}

// After (20-database-objects)
public async Task<IActionResult> UpdateItem(string id, [FromBody] MusicGroupCUdto item)
{
    try
    {
        item.EnsureValidity();    // ← throws ArgumentException on invalid input
        var idArg = Guid.Parse(id);
        ...
    }
}
```

Calling `EnsureValidity()` before any other work means bad input fails fast with a clear message, without touching the database or consuming a scoped `DbContext`.

---

## Summary of key concepts introduced

| Concept | Where |
|---|---|
| SQL view mapped to a key-less `DbSet` with `.ToView().HasNoKey()` | `MainDbContext`, `GstUsrInfoDbDto` |
| Calling a stored procedure with output parameters via raw ADO.NET (`DbCommand`) | `AdminDbRepos.RemoveSeedAsync` |
| Reusing EF Core's managed connection for raw ADO.NET (`GetDbConnection()`) | `AdminDbRepos.RemoveSeedAsync` |
| Reading a stored procedure's inline result set via `ExecuteReaderAsync` | `AdminDbRepos.RemoveSeedAsync` |
| Schema-based access control — `gstusr` view visible to least-privileged user | `initDatabase.sql`, `GuestController` |
| `EnsureValidity()` on DTOs — validation as part of the DTO's own contract | `MusicGroupCUdto`, `AlbumCUdto`, both controllers |
| `clearDatabase.sql` — dropping objects in FK-safe order without dropping the database | `DbContext/SqlScripts/sqlserver/` |
