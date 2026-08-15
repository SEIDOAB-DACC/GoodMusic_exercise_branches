# Step-by-step: from `13-schema-annotations` to `18-crud-complete`

This branch adds a full CRUD API for both `MusicGroup` and `Album`. It introduces three new patterns — **response wrapper DTOs**, **CU (Create/Update) DTOs**, and a **`navProp_*` helper** for resolving Guid foreign keys — then applies them consistently across repos, services, and controllers for both entities.

Work through the steps in order.

---

## Step 1 — Create `Models/DTO/ResponseDto.cs`

Create the `DTO/` subdirectory inside `Models/` and add the two generic wrapper types used as the return type of every service and controller method.

`ResponsePageDto<T>` carries a page of items together with the total row count and pagination metadata so the client never needs a second request to know how many pages exist.  
`ConnectionString` is only compiled into debug builds — it surfaces the password-stripped connection string added to `MainDbContext` in the previous branch.

```csharp
namespace Models.DTO;

public class ResponsePageDto<T>
{
#if DEBUG
    public string ConnectionString { get; init; }
#endif
    public List<T> PageItems { get; init; }
    public int DbItemsCount { get; init; }
    public int PageNr { get; init; }
    public int PageSize { get; init; }
    public int PageCount => (int)Math.Ceiling((double)DbItemsCount / PageSize);
}

public class ResponseItemDto<T>
{
#if DEBUG
    public string ConnectionString { get; init; }
#endif
    public T Item { get; init; }
}
```

---

## Step 2 — Create `Models/DTO/CuDto.cs`

A CU DTO is what the client sends in the request body for create and update operations. Related entities are expressed as **lists of Guids**, not nested objects — the repo resolves them to tracked EF Core instances. Each DTO also has a constructor that converts from the domain interface, which the `ReadItemDto` endpoint uses to pre-populate an edit form.

```csharp
using System.Text.RegularExpressions;
using Models.Interfaces;

namespace Models.DTO;

public class MusicGroupCUdto
{
    public Guid? MusicGroupId { get; set; }    // null on create, set on update
    public bool Seeded { get; set; } = true;
    public string Name { get; set; }
    public int EstablishedYear { get; set; }
    public MusicGenre Genre { get; set; }
    public List<Guid> AlbumsId { get; set; } = new List<Guid>();

    public MusicGroupCUdto() { }
    public MusicGroupCUdto(IMusicGroup model)
    {
        this.MusicGroupId = model.MusicGroupId;
        this.Name = model.Name;
        this.AlbumsId = model.Albums.Select(a => a.AlbumId).ToList();
    }
}

public class AlbumCUdto
{
    public Guid? AlbumId { get; set; }
    public bool Seeded { get; set; } = true;
    public string Name { get; set; }
    public Guid MusicGroupId { get; set; }     // FK carried as a Guid, not a nested object

    public AlbumCUdto() { }
    public AlbumCUdto(IAlbum model)
    {
        this.AlbumId = model.AlbumId;
        this.Name = model.Name;
        this.MusicGroupId = model.MusicGroup.MusicGroupId;
    }
}
```

---

## Step 3 — Update `DbModels/MusicGroupDbM.cs`

Add `using Models.DTO;`, then add a DTO constructor and an `UpdateFromDTO` method. Navigation properties are deliberately excluded from `UpdateFromDTO` — they require async DB lookups and are handled separately by the `navProp_*` methods in the repo.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;
using Models.DTO;

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
    public MusicGroupDbM()
    {
        MusicGroupId = Guid.NewGuid();
    }
    public MusicGroupDbM(MusicGroupCUdto dto) : this()
    {
        UpdateFromDTO(dto);
    }
    #endregion

    #region Update from DTO
    public MusicGroupDbM UpdateFromDTO(MusicGroupCUdto dto)
    {
        Name = dto.Name;
        return this;
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

## Step 4 — Update `DbModels/AlbumDbM.cs`

Same pattern as `MusicGroupDbM`: add `using Models.DTO;`, a DTO constructor, and `UpdateFromDTO`. Also add `AlbumId = Guid.NewGuid();` to the default constructor (it was missing in branch 13):

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;
using Models.DTO;

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
    public AlbumDbM()
    {
        AlbumId = Guid.NewGuid();
    }
    public AlbumDbM(AlbumCUdto dto) : this()
    {
        UpdateFromDTO(dto);
    }
    #endregion

    #region Update from DTO
    public AlbumDbM UpdateFromDTO(AlbumCUdto dto)
    {
        Name = dto.Name;
        return this;
    }
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

## Step 5 — Add `using Models.DTO` to `DbContext/MainDbContext.cs`

The `DbContext` project references `Models`, and the `dbConnection` debug property type is used in the repo response constructors. Add the single missing using:

```csharp
using Configuration;
using Models.DTO;     // ← add this line
using DbModels;
using Microsoft.Extensions.Hosting.Internal;
using DbContext.Extensions;
```

---

## Step 6 — Create `DbRepos/MusicGroupsDbRepos.cs`

This file implements the full data-access layer for `MusicGroup`. Read the patterns carefully — they repeat identically in the Albums repo.

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Models.Interfaces;
using Models.DTO;
using DbModels;
using DbContext;

namespace DbRepos;

public class MusicGroupsDbRepos
{
    private ILogger<MusicGroupsDbRepos> _logger;
    private readonly MainDbContext _dbContext;

    public MusicGroupsDbRepos(ILogger<MusicGroupsDbRepos> logger, MainDbContext context)
    {
        _logger = logger;
        _dbContext = context;
    }

    public async Task<ResponseItemDto<IMusicGroup>> ReadMusicGroupAsync(Guid id, bool flat)
    {
        IMusicGroup item;
        if (!flat)
        {
            // AsNoTracking: no change tracking needed on reads — improves performance and avoids circular access
            var query = _dbContext.MusicGroups.AsNoTracking()
                .Include(i => i.AlbumsDbM)
                .Where(i => i.MusicGroupId == id);
            item = await query.FirstOrDefaultAsync<IMusicGroup>();
        }
        else
        {
            var query = _dbContext.MusicGroups.AsNoTracking()
                .Where(i => i.MusicGroupId == id);
            item = await query.FirstOrDefaultAsync<IMusicGroup>();
        }

        if (item == null) throw new ArgumentException($"Item {id} is not existing");
        return new ResponseItemDto<IMusicGroup>()
        {
#if DEBUG
            ConnectionString = _dbContext.dbConnection,
#endif
            Item = item
        };
    }

    public async Task<ResponsePageDto<IMusicGroup>> ReadMusicGroupsAsync(
        bool seeded, bool flat, string filter, int pageNumber, int pageSize)
    {
        filter ??= "";
        IQueryable<MusicGroupDbM> query = flat
            ? _dbContext.MusicGroups.AsNoTracking()
            : _dbContext.MusicGroups.AsNoTracking().Include(i => i.AlbumsDbM);

        var ret = new ResponsePageDto<IMusicGroup>()
        {
#if DEBUG
            ConnectionString = _dbContext.dbConnection,
#endif
            DbItemsCount = await query
                .Where(i => (i.Seeded == seeded) && (i.Name.ToLower().Contains(filter)))
                .CountAsync(),

            PageItems = await query
                .Where(i => (i.Seeded == seeded) && (i.Name.ToLower().Contains(filter)))
                .Skip(pageNumber * pageSize)
                .Take(pageSize)
                .ToListAsync<IMusicGroup>(),

            PageNr = pageNumber,
            PageSize = pageSize
        };
        return ret;
    }

    public async Task<ResponseItemDto<IMusicGroup>> DeleteMusicGroupAsync(Guid id)
    {
        var item = await _dbContext.MusicGroups
            .Where(i => i.MusicGroupId == id)
            .FirstOrDefaultAsync<MusicGroupDbM>();

        if (item == null) throw new ArgumentException($"Item {id} is not existing");

        _dbContext.MusicGroups.Remove(item);
        await _dbContext.SaveChangesAsync();

        return new ResponseItemDto<IMusicGroup>()
        {
#if DEBUG
            ConnectionString = _dbContext.dbConnection,
#endif
            Item = item
        };
    }

    public async Task<ResponseItemDto<IMusicGroup>> UpdateMusicGroupAsync(MusicGroupCUdto itemDto)
    {
        var item = await _dbContext.MusicGroups
            .Where(i => i.MusicGroupId == itemDto.MusicGroupId)
            .Include(i => i.AlbumsDbM)
            .FirstOrDefaultAsync<MusicGroupDbM>();

        if (item == null) throw new ArgumentException($"Item {itemDto.MusicGroupId} is not existing");

        item.UpdateFromDTO(itemDto);
        await navProp_MusicGroupCUdto_To_MusicGroup(itemDto, item);

        _dbContext.MusicGroups.Update(item);
        await _dbContext.SaveChangesAsync();

        // read back the saved item fully populated
        return await ReadMusicGroupAsync(item.MusicGroupId, false);
    }

    public async Task<ResponseItemDto<IMusicGroup>> CreateMusicGroupAsync(MusicGroupCUdto itemDto)
    {
        if (itemDto.MusicGroupId != null)
            throw new ArgumentException($"{nameof(itemDto.MusicGroupId)} must be null when creating a new object");

        itemDto.Seeded = false;
        var item = new MusicGroupDbM(itemDto);
        await navProp_MusicGroupCUdto_To_MusicGroup(itemDto, item);

        _dbContext.MusicGroups.Add(item);
        await _dbContext.SaveChangesAsync();

        return await ReadMusicGroupAsync(item.MusicGroupId, false);
    }

    // resolves each Album Guid in the DTO to a tracked AlbumDbM and assigns the list
    private async Task navProp_MusicGroupCUdto_To_MusicGroup(
        MusicGroupCUdto itemDtoSrc, MusicGroupDbM itemDst)
    {
        List<AlbumDbM> albums = new List<AlbumDbM>();
        foreach (var id in itemDtoSrc.AlbumsId)
        {
            var album = await _dbContext.Albums.FirstOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
                throw new ArgumentException($"Item id {id} not existing");
            albums.Add(album);
        }
        itemDst.AlbumsDbM = albums;
    }
}
```

---

## Step 7 — Create `DbRepos/AlbumsDbRepos.cs`

The same structure as `MusicGroupsDbRepos`, applied to `Album`. The `navProp_*` helper resolves the single `MusicGroupId` Guid instead of a list:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Models.DTO;
using DbModels;
using DbContext;
using Models.Interfaces;

namespace DbRepos;

public class AlbumsDbRepos
{
    private ILogger<AlbumsDbRepos> _logger;
    private readonly MainDbContext _dbContext;

    public AlbumsDbRepos(ILogger<AlbumsDbRepos> logger, MainDbContext context)
    {
        _logger = logger;
        _dbContext = context;
    }

    public async Task<ResponseItemDto<IAlbum>> ReadAlbumAsync(Guid id, bool flat)
    {
        IAlbum item;
        if (!flat)
        {
            var query = _dbContext.Albums.AsNoTracking()
                .Include(i => i.MusicGroupDbM)
                .Where(i => i.AlbumId == id);
            item = await query.FirstOrDefaultAsync<IAlbum>();
        }
        else
        {
            var query = _dbContext.Albums.AsNoTracking()
                .Where(i => i.AlbumId == id);
            item = await query.FirstOrDefaultAsync<IAlbum>();
        }

        if (item == null) throw new ArgumentException($"Item {id} is not existing");
        return new ResponseItemDto<IAlbum>()
        {
#if DEBUG
            ConnectionString = _dbContext.dbConnection,
#endif
            Item = item
        };
    }

    public async Task<ResponsePageDto<IAlbum>> ReadAlbumsAsync(
        bool seeded, bool flat, string filter, int pageNumber, int pageSize)
    {
        filter ??= "";
        IQueryable<AlbumDbM> query = flat
            ? _dbContext.Albums.AsNoTracking()
            : _dbContext.Albums.AsNoTracking().Include(i => i.MusicGroupDbM);

        var ret = new ResponsePageDto<IAlbum>()
        {
#if DEBUG
            ConnectionString = _dbContext.dbConnection,
#endif
            DbItemsCount = await query
                .Where(i => (i.Seeded == seeded) && (i.Name.ToLower().Contains(filter)))
                .CountAsync(),

            PageItems = await query
                .Where(i => (i.Seeded == seeded) && (i.Name.ToLower().Contains(filter)))
                .Skip(pageNumber * pageSize)
                .Take(pageSize)
                .ToListAsync<IAlbum>(),

            PageNr = pageNumber,
            PageSize = pageSize
        };
        return ret;
    }

    public async Task<ResponseItemDto<IAlbum>> DeleteAlbumAsync(Guid id)
    {
        var item = await _dbContext.Albums
            .Where(i => i.AlbumId == id)
            .FirstOrDefaultAsync<AlbumDbM>();

        if (item == null) throw new ArgumentException($"Item {id} is not existing");

        _dbContext.Albums.Remove(item);
        await _dbContext.SaveChangesAsync();

        return new ResponseItemDto<IAlbum>()
        {
#if DEBUG
            ConnectionString = _dbContext.dbConnection,
#endif
            Item = item
        };
    }

    public async Task<ResponseItemDto<IAlbum>> UpdateAlbumAsync(AlbumCUdto itemDto)
    {
        var item = await _dbContext.Albums
            .Where(i => i.AlbumId == itemDto.AlbumId)
            .Include(i => i.MusicGroupDbM)
            .FirstOrDefaultAsync<AlbumDbM>();

        if (item == null) throw new ArgumentException($"Item {itemDto.AlbumId} is not existing");

        item.UpdateFromDTO(itemDto);
        await navProp_AlbumCUdto_to_AlbumDbM(itemDto, item);

        _dbContext.Albums.Update(item);
        await _dbContext.SaveChangesAsync();

        return await ReadAlbumAsync(item.AlbumId, false);
    }

    public async Task<ResponseItemDto<IAlbum>> CreateAlbumAsync(AlbumCUdto itemDto)
    {
        if (itemDto.AlbumId != null)
            throw new ArgumentException($"{nameof(itemDto.AlbumId)} must be null when creating a new object");

        itemDto.Seeded = false;
        var item = new AlbumDbM(itemDto);
        await navProp_AlbumCUdto_to_AlbumDbM(itemDto, item);

        _dbContext.Albums.Add(item);
        await _dbContext.SaveChangesAsync();

        return await ReadAlbumAsync(item.AlbumId, false);
    }

    // resolves the single MusicGroupId Guid in the DTO to a tracked MusicGroupDbM and assigns it
    private async Task navProp_AlbumCUdto_to_AlbumDbM(AlbumCUdto itemDtoSrc, AlbumDbM itemDst)
    {
        var musicGroup = await _dbContext.MusicGroups
            .FirstOrDefaultAsync(a => a.MusicGroupId == itemDtoSrc.MusicGroupId);
        if (musicGroup == null)
            throw new ArgumentException($"Item id {itemDtoSrc.MusicGroupId} not existing");
        itemDst.MusicGroupDbM = musicGroup;
    }
}
```

---

## Step 8 — Update `Services/Interfaces.cs`

Add `IMusicGroupsService` and `IAlbumsService` below the existing `IAdminService`. All return types use the **interface** type (`IMusicGroup`, `IAlbum`) — controllers never see concrete `DbM` types:

```csharp
using Models.Interfaces;
using Models.DTO;

namespace Services;

public interface IAdminService
{
    public Task SeedAsync(int seedCount);
}

public interface IMusicGroupsService
{
    public Task<ResponsePageDto<IMusicGroup>> ReadMusicGroupsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize);
    public Task<ResponseItemDto<IMusicGroup>> ReadMusicGroupAsync(Guid id, bool flat);
    public Task<ResponseItemDto<IMusicGroup>> DeleteMusicGroupAsync(Guid id);
    public Task<ResponseItemDto<IMusicGroup>> UpdateMusicGroupAsync(MusicGroupCUdto item);
    public Task<ResponseItemDto<IMusicGroup>> CreateMusicGroupAsync(MusicGroupCUdto item);
}

public interface IAlbumsService
{
    public Task<ResponsePageDto<IAlbum>> ReadAlbumsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize);
    public Task<ResponseItemDto<IAlbum>> ReadAlbumAsync(Guid id, bool flat);
    public Task<ResponseItemDto<IAlbum>> DeleteAlbumAsync(Guid id);
    public Task<ResponseItemDto<IAlbum>> UpdateAlbumAsync(AlbumCUdto item);
    public Task<ResponseItemDto<IAlbum>> CreateAlbumAsync(AlbumCUdto item);
}
```

---

## Step 9 — Create `Services/MusicGroupsServiceDb.cs`

A thin pass-through. Every method delegates directly to the repo. Business logic (validation, caching, mapping) can be added here in later branches without touching the controller or repo:

```csharp
using Microsoft.Extensions.Logging;
using DbRepos;
using Models.Interfaces;
using Models.DTO;

namespace Services;

public class MusicGroupsServiceDb : IMusicGroupsService
{
    private readonly MusicGroupsDbRepos _repo;
    private readonly ILogger<MusicGroupsServiceDb> _logger;

    public MusicGroupsServiceDb(MusicGroupsDbRepos repo) { _repo = repo; }
    public MusicGroupsServiceDb(MusicGroupsDbRepos repo, ILogger<MusicGroupsServiceDb> logger) : this(repo)
    {
        _logger = logger;
    }

    public Task<ResponsePageDto<IMusicGroup>> ReadMusicGroupsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize)
        => _repo.ReadMusicGroupsAsync(seeded, flat, filter, pageNumber, pageSize);
    public Task<ResponseItemDto<IMusicGroup>> ReadMusicGroupAsync(Guid id, bool flat)
        => _repo.ReadMusicGroupAsync(id, flat);
    public Task<ResponseItemDto<IMusicGroup>> DeleteMusicGroupAsync(Guid id)
        => _repo.DeleteMusicGroupAsync(id);
    public Task<ResponseItemDto<IMusicGroup>> UpdateMusicGroupAsync(MusicGroupCUdto item)
        => _repo.UpdateMusicGroupAsync(item);
    public Task<ResponseItemDto<IMusicGroup>> CreateMusicGroupAsync(MusicGroupCUdto item)
        => _repo.CreateMusicGroupAsync(item);
}
```

---

## Step 10 — Create `Services/AlbumsServiceDb.cs`

```csharp
using Microsoft.Extensions.Logging;
using DbRepos;
using Models.Interfaces;
using Models.DTO;

namespace Services;

public class AlbumsServiceDb : IAlbumsService
{
    private readonly AlbumsDbRepos _repo;
    private readonly ILogger<AlbumsServiceDb> _logger;

    public AlbumsServiceDb(AlbumsDbRepos repo) { _repo = repo; }
    public AlbumsServiceDb(AlbumsDbRepos repo, ILogger<AlbumsServiceDb> logger) : this(repo)
    {
        _logger = logger;
    }

    public Task<ResponsePageDto<IAlbum>> ReadAlbumsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize)
        => _repo.ReadAlbumsAsync(seeded, flat, filter, pageNumber, pageSize);
    public Task<ResponseItemDto<IAlbum>> ReadAlbumAsync(Guid id, bool flat)
        => _repo.ReadAlbumAsync(id, flat);
    public Task<ResponseItemDto<IAlbum>> DeleteAlbumAsync(Guid id)
        => _repo.DeleteAlbumAsync(id);
    public Task<ResponseItemDto<IAlbum>> UpdateAlbumAsync(AlbumCUdto item)
        => _repo.UpdateAlbumAsync(item);
    public Task<ResponseItemDto<IAlbum>> CreateAlbumAsync(AlbumCUdto item)
        => _repo.CreateAlbumAsync(item);
}
```

---

## Step 11 — Create `AppWebApi/Controllers/MusicGroupsController.cs`

Six endpoints. Key points:
- `Read` and `ReadItem` accept all parameters as `string` and parse them inside `try/catch` — this produces a clean `400 Bad Request` on malformed input rather than an unhandled exception.
- `filter` is validated with a regex before it reaches the repo to prevent injection.
- `ReadItemDto` reads the full item then projects it to a CU DTO — intended for pre-populating edit forms.
- `UpdateItem` guards against an Id mismatch between the route `{id}` and the DTO body.
- `CreateItem` relies on the repo to enforce that `MusicGroupId` must be `null`.

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Models.Interfaces;
using Models.DTO;
using Services;

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class MusicGroupsController : Controller
    {
        readonly IMusicGroupsService _service = null;
        readonly ILogger<MusicGroupsController> _logger = null;

        [HttpGet()]
        [ActionName("Read")]
        [ProducesResponseType(200, Type = typeof(ResponsePageDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> Read(string seeded = "true", string flat = "true",
            string filter = null, string pageNr = "0", string pageSize = "10")
        {
            try
            {
                bool seededArg = bool.Parse(seeded);
                bool flatArg = bool.Parse(flat);
                int pageNrArg = int.Parse(pageNr);
                int pageSizeArg = int.Parse(pageSize);

                // reject any filter that contains characters outside a-z, 0-9, and spaces
                if (!string.IsNullOrEmpty(filter) && !Regex.IsMatch(filter, @"^[a-zA-Z0-9\s]*$"))
                    throw new ArgumentException("Filter can only contain letters (a-z), numbers (0-9), and spaces.");

                _logger.LogInformation($"{nameof(Read)}: seeded={seededArg}, flat={flatArg}, pageNr={pageNrArg}, pageSize={pageSizeArg}");

                var resp = await _service.ReadMusicGroupsAsync(seededArg, flatArg, filter?.Trim().ToLower(), pageNrArg, pageSizeArg);
                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(Read)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet()]
        [ActionName("ReadItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        public async Task<IActionResult> ReadItem(string id = null, string flat = "false")
        {
            try
            {
                var idArg = Guid.Parse(id);
                bool flatArg = bool.Parse(flat);

                _logger.LogInformation($"{nameof(ReadItem)}: id={idArg}, flat={flatArg}");

                var item = await _service.ReadMusicGroupAsync(idArg, flatArg);
                if (item == null) throw new ArgumentException($"Item with id {id} does not exist");
                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ReadItem)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [ActionName("DeleteItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> DeleteItem(string id)
        {
            try
            {
                var idArg = Guid.Parse(id);
                _logger.LogInformation($"{nameof(DeleteItem)}: id={idArg}");

                var item = await _service.DeleteMusicGroupAsync(idArg);
                if (item == null) throw new ArgumentException($"Item with id {id} does not exist");

                _logger.LogInformation($"item {idArg} deleted");
                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(DeleteItem)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet()]
        [ActionName("ReadItemDto")]
        [ProducesResponseType(200, Type = typeof(MusicGroupCUdto))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        public async Task<IActionResult> ReadItemDto(string id = null)
        {
            try
            {
                var idArg = Guid.Parse(id);
                _logger.LogInformation($"{nameof(ReadItemDto)}: id={idArg}");

                var item = await _service.ReadMusicGroupAsync(idArg, false);
                if (item == null) throw new ArgumentException($"Item with id {id} does not exist");

                return Ok(new ResponseItemDto<MusicGroupCUdto>()
                {
#if DEBUG
                    ConnectionString = item.ConnectionString,
#endif
                    Item = new MusicGroupCUdto(item.Item)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ReadItemDto)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        [ActionName("UpdateItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> UpdateItem(string id, [FromBody] MusicGroupCUdto item)
        {
            try
            {
                var idArg = Guid.Parse(id);
                _logger.LogInformation($"{nameof(UpdateItem)}: id={idArg}");
                if (item.MusicGroupId != idArg) throw new ArgumentException("Id mismatch");

                var _item = await _service.UpdateMusicGroupAsync(item);
                _logger.LogInformation($"item {idArg} updated");
                return Ok(_item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(UpdateItem)}: {ex.Message}");
                return BadRequest($"Could not update. Error {ex.Message}");
            }
        }

        [HttpPost()]
        [ActionName("CreateItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> CreateItem([FromBody] MusicGroupCUdto item)
        {
            try
            {
                _logger.LogInformation($"{nameof(CreateItem)}:");

                var _item = await _service.CreateMusicGroupAsync(item);
                _logger.LogInformation($"item {_item.Item.MusicGroupId} created");
                return Ok(_item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(CreateItem)}: {ex.Message}");
                return BadRequest($"Could not create. Error {ex.Message}");
            }
        }

        public MusicGroupsController(IMusicGroupsService service, ILogger<MusicGroupsController> logger)
        {
            _service = service;
            _logger = logger;
        }
    }
}
```

---

## Step 12 — Create `AppWebApi/Controllers/AlbumsController.cs`

Identical structure to `MusicGroupsController`, operating on `IAlbum` / `AlbumCUdto`:

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Models.Interfaces;
using Models.DTO;
using Services;

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AlbumsController : Controller
    {
        readonly IAlbumsService _service;
        readonly ILogger<AlbumsController> _logger;

        [HttpGet()]
        [ActionName("Read")]
        [ProducesResponseType(200, Type = typeof(ResponsePageDto<IAlbum>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> Read(string seeded = "true", string flat = "true",
            string filter = null, string pageNr = "0", string pageSize = "10")
        {
            try
            {
                bool seededArg = bool.Parse(seeded);
                bool flatArg = bool.Parse(flat);
                int pageNrArg = int.Parse(pageNr);
                int pageSizeArg = int.Parse(pageSize);

                if (!string.IsNullOrEmpty(filter) && !Regex.IsMatch(filter, @"^[a-zA-Z0-9\s]*$"))
                    throw new ArgumentException("Filter can only contain letters (a-z), numbers (0-9), and spaces.");

                _logger.LogInformation($"{nameof(Read)}: seeded={seededArg}, flat={flatArg}, pageNr={pageNrArg}, pageSize={pageSizeArg}");

                var resp = await _service.ReadAlbumsAsync(seededArg, flatArg, filter?.Trim().ToLower(), pageNrArg, pageSizeArg);
                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(Read)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet()]
        [ActionName("ReadItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IAlbum>))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        public async Task<IActionResult> ReadItem(string id = null, string flat = "false")
        {
            try
            {
                var idArg = Guid.Parse(id);
                bool flatArg = bool.Parse(flat);

                _logger.LogInformation($"{nameof(ReadItem)}: id={idArg}, flat={flatArg}");

                var item = await _service.ReadAlbumAsync(idArg, flatArg);
                if (item == null) throw new ArgumentException($"Item with id {id} does not exist");
                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ReadItem)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [ActionName("DeleteItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IAlbum>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> DeleteItem(string id)
        {
            try
            {
                var idArg = Guid.Parse(id);
                _logger.LogInformation($"{nameof(DeleteItem)}: id={idArg}");

                var item = await _service.DeleteAlbumAsync(idArg);
                if (item == null) throw new ArgumentException($"Item with id {id} does not exist");

                _logger.LogInformation($"item {idArg} deleted");
                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(DeleteItem)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet()]
        [ActionName("ReadItemDto")]
        [ProducesResponseType(200, Type = typeof(AlbumCUdto))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        public async Task<IActionResult> ReadItemDto(string id = null)
        {
            try
            {
                var idArg = Guid.Parse(id);
                _logger.LogInformation($"{nameof(ReadItemDto)}: id={idArg}");

                var item = await _service.ReadAlbumAsync(idArg, false);
                if (item == null) throw new ArgumentException($"Item with id {id} does not exist");

                return Ok(new ResponseItemDto<AlbumCUdto>()
                {
#if DEBUG
                    ConnectionString = item.ConnectionString,
#endif
                    Item = new AlbumCUdto(item.Item)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ReadItemDto)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        [ActionName("UpdateItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IAlbum>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> UpdateItem(string id, [FromBody] AlbumCUdto item)
        {
            try
            {
                var idArg = Guid.Parse(id);
                _logger.LogInformation($"{nameof(UpdateItem)}: id={idArg}");
                if (item.AlbumId != idArg) throw new ArgumentException("Id mismatch");

                var model = await _service.UpdateAlbumAsync(item);
                _logger.LogInformation($"item {idArg} updated");
                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(UpdateItem)}: {ex.Message}");
                return BadRequest($"Could not update. Error {ex.Message}");
            }
        }

        [HttpPost()]
        [ActionName("CreateItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IAlbum>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> CreateItem([FromBody] AlbumCUdto item)
        {
            try
            {
                _logger.LogInformation($"{nameof(CreateItem)}:");

                var model = await _service.CreateAlbumAsync(item);
                _logger.LogInformation($"item {model.Item.AlbumId} created");
                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(CreateItem)}: {ex.Message}");
                return BadRequest($"Could not create. Error {ex.Message}");
            }
        }

        public AlbumsController(IAlbumsService service, ILogger<AlbumsController> logger)
        {
            _service = service;
            _logger = logger;
        }
    }
}
```

---

## Step 13 — Update `AppWebApi/Program.cs`

Add four registrations after the existing `AdminDbRepos` and `IAdminService` lines. All are `Scoped` to match the lifetime of `MainDbContext`:

```csharp
// existing
builder.Services.AddScoped<AdminDbRepos>();

// new
builder.Services.AddScoped<MusicGroupsDbRepos>();
builder.Services.AddScoped<AlbumsDbRepos>();

// existing
builder.Services.AddScoped<IAdminService, AdminServiceDb>();

// new
builder.Services.AddScoped<IMusicGroupsService, MusicGroupsServiceDb>();
builder.Services.AddScoped<IAlbumsService, AlbumsServiceDb>();
```

Also add the missing `using` for the new service types if your IDE doesn't add them automatically:

```csharp
using Services;   // already present — covers all types in the Services namespace
```

---

## Verification

Build and run the project, then open Swagger (`/swagger`). You should see three controller groups:

- **Admin** — `Seed`, `Log`, `Environment`, `Version`
- **MusicGroups** — `Read`, `ReadItem`, `ReadItemDto`, `DeleteItem`, `UpdateItem`, `CreateItem`
- **Albums** — same six endpoints

Test the flow end-to-end:

1. `POST Admin/Seed?seedCount=5` — populate the database
2. `GET MusicGroups/Read?seeded=true&flat=false&pageNr=0&pageSize=5` — verify paged response with albums included
3. Copy a `MusicGroupId` from the response
4. `GET MusicGroups/ReadItemDto?id=<guid>` — get the CU DTO for that item
5. `PUT MusicGroups/UpdateItem/<guid>` — send the DTO back with a modified `Name`
6. `POST Albums/CreateItem` — body: `{ "name": "New Album", "musicGroupId": "<guid>" }`
7. `DELETE Albums/DeleteItem/<albumGuid>` — verify the album is removed
