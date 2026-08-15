# Step-by-step: from `18-crud-complete` to `20-database-objects`

This branch adds three things that live **outside** EF Core migrations: a SQL view, a stored procedure, and the C# code that calls them. It also adds input validation on write operations and a read-only guest controller.

Work through the steps in order.

---

## Step 1 — Create `DbContext/SqlScripts/sqlserver/initDatabase.sql`

Create the directory `DbContext/SqlScripts/sqlserver/` and add this file. It is run manually after the EF Core migration has created the tables — EF Core does not manage views or stored procedures.

```sql
USE [sql-music];
GO

-- create schemas
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'gstusr')
    EXEC('CREATE SCHEMA gstusr');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'usr')
    EXEC('CREATE SCHEMA usr');
GO

-- view: single row of aggregate counts, readable by the gstusr database user
CREATE OR ALTER VIEW gstusr.vwInfoDb AS
    SELECT
        (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 1) as NrSeededMusicGroups,
        (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 0) as NrUnseededMusicGroups,
        (SELECT COUNT(*) FROM supusr.Albums WHERE Seeded = 1)      as NrSeededAlbums,
        (SELECT COUNT(*) FROM supusr.Albums WHERE Seeded = 0)      as NrUnseededAlbums;
GO

-- stored procedure: deletes rows by Seeded flag, returns view state as a result set
CREATE OR ALTER PROC supusr.spDeleteAll
    @seededParam           BIT = 1,
    @nrMusicGroupsAffected INT OUTPUT,
    @nrAlbumsAffected      INT OUTPUT
AS
    SET NOCOUNT ON;

    SELECT @nrMusicGroupsAffected = COUNT(*) FROM supusr.MusicGroups WHERE Seeded = @seededParam;
    SELECT @nrAlbumsAffected      = COUNT(*) FROM supusr.Albums      WHERE Seeded = @seededParam;

    DELETE FROM supusr.MusicGroups WHERE Seeded = @seededParam;
    DELETE FROM supusr.Albums      WHERE Seeded = @seededParam;

    SELECT * FROM gstusr.vwInfoDb;
GO
```

Run this script from Azure Data Studio or `sqlcmd` after every fresh migration.

---

## Step 2 — Create `DbContext/SqlScripts/sqlserver/clearDatabase.sql`

This script tears down all objects created by `initDatabase.sql` in the correct order. Use it instead of dropping the whole database when you need a clean slate:

```sql
USE [sql-music];
--GO

-- remove stored procedures
DROP PROCEDURE IF EXISTS supusr.spDeleteAll
GO

-- remove views
DROP VIEW IF EXISTS [gstusr].[vwInfoDb]
GO

-- drop tables in FK order to avoid constraint conflicts
DROP TABLE IF EXISTS supusr.ArtistDbMMusicGroupDbM;
DROP TABLE IF EXISTS supusr.Albums;
DROP TABLE IF EXISTS supusr.MusicGroups;
DROP TABLE IF EXISTS __EFMigrationsHistory;
GO
```

---

## Step 3 — Create `Models/DTO/GstUsrDto.cs`

`GstUsrInfoDbDto` is mapped by EF Core to the view — property names match the column aliases in the `SELECT`. `GstUsrInfoAllDto` is the API response envelope:

```csharp
namespace Models.DTO;

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

---

## Step 4 — Add `EnsureValidity()` to `Models/DTO/CuDto.cs`

Add a validation method to each DTO. This keeps validation logic inside the DTO itself rather than scattered across controllers.

Add to `MusicGroupCUdto`, after the constructor:

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

Add to `AlbumCUdto`, after the constructor:

```csharp
public void EnsureValidity()
{
    if (!string.IsNullOrEmpty(Name) && !Regex.IsMatch(Name, @"^[a-zA-Z0-9\s\-\.!]*$"))
        throw new ArgumentException("Name can only contain letters (a-z), numbers (0-9), spaces, -, ., and !.");
}
```

---

## Step 5 — Update `DbContext/MainDbContext.cs`

Two additions are needed to expose the view through EF Core.

Add `using Models.DTO;` if not already present, then add the `DbSet` alongside the table `DbSet`s:

```csharp
    #region C# model of database tables
    public DbSet<MusicGroupDbM> MusicGroups { get; set; }
    public DbSet<AlbumDbM> Albums { get; set; }
    #endregion

    #region model the Views
    public DbSet<GstUsrInfoDbDto> InfoDbView { get; set; }
    #endregion
```

Then register it in `OnModelCreating` with no key — views have no primary key:

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        #region model the Views
        modelBuilder.Entity<GstUsrInfoDbDto>().ToView("vwInfoDb", "gstusr").HasNoKey();
        #endregion

        #region override modelbuilder
        #endregion

        base.OnModelCreating(modelBuilder);
    }
```

---

## Step 6 — Update `DbRepos/AdminDbRepos.cs`

Add these imports at the top:

```csharp
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Models.DTO;
```

Then add three methods before the constructor.

### `RemoveSeedAsync` — calls the stored procedure via raw ADO.NET

EF Core has no built-in support for stored procedures with output parameters, so this method uses the connection EF Core already manages:

```csharp
public async Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded)
{
    var connection = _dbContext.Database.GetDbConnection();
    using var command = connection.CreateCommand();
    command.CommandType = CommandType.StoredProcedure;
    command.CommandText = "supusr.spDeleteAll";

    List<DbParameter> parameters = new List<DbParameter>
    {
        new SqlParameter("seededParam",           seeded),
        new SqlParameter("nrMusicGroupsAffected", SqlDbType.Int) { Direction = ParameterDirection.Output },
        new SqlParameter("nrAlbumsAffected",      SqlDbType.Int) { Direction = ParameterDirection.Output }
    };
    command.Parameters.AddRange(parameters.ToArray());

    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    using var reader = await command.ExecuteReaderAsync();

    // read the result set the procedure returns (SELECT * FROM gstusr.vwInfoDb)
    GstUsrInfoDbDto result_set = null;
    if (reader.HasRows)
    {
        await reader.ReadAsync();
        result_set = new GstUsrInfoDbDto
        {
            NrSeededMusicGroups   = Convert.ToInt32(reader["NrSeededMusicGroups"]),
            NrUnseededMusicGroups = Convert.ToInt32(reader["NrUnseededMusicGroups"]),
            NrSeededAlbums        = Convert.ToInt32(reader["NrSeededAlbums"]),
            NrUnseededAlbums      = Convert.ToInt32(reader["NrUnseededAlbums"])
        };
        await reader.CloseAsync();
    }

    return await DbInfo();
}
```

`_dbContext.Database.GetDbConnection()` reuses EF Core's connection so no second connection is opened. After the reader is closed the output parameters are available on the `SqlParameter` objects, though they are not used in the return value — `DbInfo()` re-reads the view via EF Core to keep the response path consistent.

### `InfoAsync` and `DbInfo`

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

`_dbContext.InfoDbView.FirstAsync()` issues `SELECT TOP 1 * FROM gstusr.vwInfoDb`. The view always returns one row so `FirstAsync` is appropriate.

---

## Step 7 — Update `Services/Interfaces.cs`

Add two methods to `IAdminService`:

```csharp
public interface IAdminService
{
    public Task SeedAsync(int seedCount);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> GuestInfoAsync();
}
```

Add `using Models.DTO;` at the top of the file if not already present.

---

## Step 8 — Update `Services/AdminServiceDb.cs`

Add `using Models.DTO;` and delegate the two new methods:

```csharp
public Task SeedAsync(int seedCount) => _repo.SeedAsync(seedCount);
public Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded) => _repo.RemoveSeedAsync(seeded);
public Task<ResponseItemDto<GstUsrInfoAllDto>> GuestInfoAsync()             => _repo.InfoAsync();
```

---

## Step 9 — Add `RemoveSeed` endpoint to `AppWebApi/Controllers/AdminController.cs`

Add `using Models.DTO;` at the top, then insert the new action between the `Seed` and `Log` methods:

```csharp
//GET: api/admin/removeseed
[HttpGet()]
[ActionName("RemoveSeed")]
[ProducesResponseType(200, Type = typeof(GstUsrInfoAllDto))]
[ProducesResponseType(400, Type = typeof(string))]
public async Task<IActionResult> RemoveSeed(string seeded = "true")
{
    try
    {
        bool seededArg = bool.Parse(seeded);
        _logger.LogInformation($"{nameof(RemoveSeed)}: {nameof(seededArg)}: {seededArg}");
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

`seeded=true` (default) deletes seed data; `seeded=false` would delete user-created rows.

---

## Step 10 — Create `AppWebApi/Controllers/GuestController.cs`

A new controller with a single read-only endpoint. It reuses `IAdminService` — no new service interface is needed because `GuestInfoAsync` is the only operation required:

```csharp
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Models.DTO;
using Services;
using System.Text.RegularExpressions;

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class GuestController : Controller
    {
        readonly IAdminService _service;
        readonly ILogger<GuestController> _logger = null;

        //GET: api/guest/info
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

        public GuestController(IAdminService service, ILogger<GuestController> logger)
        {
            _service = service;
            _logger = logger;
        }
    }
}
```

---

## Step 11 — Call `EnsureValidity()` in `MusicGroupsController` and `AlbumsController`

In both controllers, add `item.EnsureValidity();` as the **first line** inside the `try` block of both `UpdateItem` and `CreateItem`. This rejects invalid input before any parsing or database access.

**`MusicGroupsController` — `UpdateItem`:**

```csharp
public async Task<IActionResult> UpdateItem(string id, [FromBody] MusicGroupCUdto item)
{
    try
    {
        item.EnsureValidity();              // ← add this line
        var idArg = Guid.Parse(id);
        ...
```

**`MusicGroupsController` — `CreateItem`:**

```csharp
public async Task<IActionResult> CreateItem([FromBody] MusicGroupCUdto item)
{
    try
    {
        item.EnsureValidity();              // ← add this line
        _logger.LogInformation($"{nameof(CreateItem)}:");
        ...
```

Apply the same two changes in `AlbumsController` for `AlbumCUdto`.

---

## Step 12 — Run `initDatabase.sql`

After rebuilding the database with the script (or after any fresh `dotnet ef database update`), run the init script against the database:

```bash
# from Azure Data Studio: open the file and execute it
# or via sqlcmd:
sqlcmd -S localhost,14333 -U sa -P <password> -d sql-music -i DbContext/SqlScripts/sqlserver/initDatabase.sql
```

Then verify in Azure Data Studio:
- The `gstusr` and `usr` schemas exist
- `gstusr.vwInfoDb` is listed under Views
- `supusr.spDeleteAll` is listed under Stored Procedures

---

## Verification

Build and run, then open Swagger. You should now see:

- **Guest** — `Info` (returns view counts)
- **Admin** — `RemoveSeed` alongside the existing endpoints

Test the flow:

1. `GET /api/admin/Seed?seedCount=10` — populate the database
2. `GET /api/guest/Info` — confirm counts show seeded rows
3. `GET /api/admin/RemoveSeed?seeded=true` — delete seeded rows
4. `GET /api/guest/Info` again — counts should now be zero for seeded rows
5. `POST /api/MusicGroups/CreateItem` with `"establishedYear": 0` — should return `400` from `EnsureValidity()`
