# Code differences: `13-schema-annotations` → `18-crud-complete`

This branch adds a complete CRUD API for both `MusicGroup` and `Album`. The architecture gains two new horizontal layers of files and three new code patterns that run consistently across all entities: **DTOs**, **paged/filtered read**, and **navigation-property resolution**.

---

## 1. New DTOs — `Models/DTO/`

Two new files are added in a `DTO/` subdirectory of the `Models` project.

### `Models/DTO/ResponseDto.cs`

Two generic wrapper types used as the return type of every service and controller method:

```csharp
public class ResponsePageDto<T>
{
#if DEBUG
    public string ConnectionString { get; init; }  // active DB connection (no password)
#endif
    public List<T> PageItems { get; init; }
    public int DbItemsCount { get; init; }         // total rows matching the filter
    public int PageNr { get; init; }
    public int PageSize { get; init; }
    public int PageCount => (int)Math.Ceiling((double)DbItemsCount / PageSize);  // computed
}

public class ResponseItemDto<T>
{
#if DEBUG
    public string ConnectionString { get; init; }
#endif
    public T Item { get; init; }
}
```

`ResponsePageDto` carries both the current page of items **and** the total count — the client can compute how many pages exist without an extra request.  
`ConnectionString` is only compiled in debug builds; it surfaces the `dbConnection` property added to `MainDbContext` in the previous branch.

### `Models/DTO/CuDto.cs`

Two Create/Update DTOs — one per entity. A CU (Create/Update) DTO is what the client sends in the request body; it contains scalar properties plus **lists of Guids** for related entities instead of nested objects:

```csharp
public class MusicGroupCUdto
{
    public Guid? MusicGroupId { get; set; }   // null on create, set on update
    public bool Seeded { get; set; } = true;
    public string Name { get; set; }
    public int EstablishedYear { get; set; }
    public MusicGenre Genre { get; set; }
    public List<Guid> AlbumsId { get; set; } = new List<Guid>();

    public MusicGroupCUdto() { }
    public MusicGroupCUdto(IMusicGroup model)          // convert from domain model to DTO
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
    public Guid MusicGroupId { get; set; }    // foreign key as a Guid, not a nested object

    public AlbumCUdto() { }
    public AlbumCUdto(IAlbum model)
    {
        this.AlbumId = model.AlbumId;
        this.Name = model.Name;
        this.MusicGroupId = model.MusicGroup.MusicGroupId;
    }
}
```

Using Guid lists instead of nested objects prevents accidental circular serialisation and keeps the request body small. The repo is responsible for resolving the Guids back to tracked EF Core entities.

---

## 2. DbModels gain DTO constructors and `UpdateFromDTO`

Both `MusicGroupDbM` and `AlbumDbM` are extended with:

- A constructor that accepts a CU DTO — used by `Create` operations
- An `UpdateFromDTO` method — used by `Update` operations

This keeps all property-mapping logic inside the model class rather than scattered across repos.

### `DbModels/MusicGroupDbM.cs`

```csharp
public MusicGroupDbM()
{
    MusicGroupId = Guid.NewGuid();    // always assign a new Id on construction
}
public MusicGroupDbM(MusicGroupCUdto dto) : this()
{
    UpdateFromDTO(dto);
}

public MusicGroupDbM UpdateFromDTO(MusicGroupCUdto dto)
{
    Name = dto.Name;
    return this;      // fluent return for chaining
}
```

### `DbModels/AlbumDbM.cs`

```csharp
public AlbumDbM()
{
    AlbumId = Guid.NewGuid();
}
public AlbumDbM(AlbumCUdto dto) : this()
{
    UpdateFromDTO(dto);
}

public AlbumDbM UpdateFromDTO(AlbumCUdto dto)
{
    Name = dto.Name;
    return this;
}
```

Note: navigation properties (`AlbumsDbM`, `MusicGroupDbM`) are **not** set in `UpdateFromDTO` — that is deliberately handled by the `navProp_*` methods in the repos (see below), because resolving them requires an async database lookup.

---

## 3. New repo files

Two new repos replace the single `AdminDbRepos` as the data-access layer for reads and mutations. Each implements the full CRUD surface for one entity.

### Common patterns in both repos

**`AsNoTracking()` on all reads**  
EF Core normally tracks every object it loads so it can detect changes later. For read-only queries this is wasteful and can cause circular-reference exceptions. Every read query calls `.AsNoTracking()`:

```csharp
var query = _dbContext.MusicGroups.AsNoTracking()
    .Include(i => i.AlbumsDbM)
    .Where(i => i.MusicGroupId == id);
```

**`flat` parameter controls `.Include()`**  
All read methods accept a `bool flat` parameter. When `flat == false`, related entities are eagerly loaded with `.Include()`; when `flat == true` the join is skipped. This lets the caller choose between a full object graph and a lighter query.

**`seeded` and `filter` parameters on list reads**  
The paged list methods filter by `Seeded` (to separate test data from real data) and by a substring filter on `Name`:

```csharp
.Where(i => (i.Seeded == seeded) && (i.Name.ToLower().Contains(filter)))
```

**Pagination with `.Skip().Take()`**

```csharp
.Skip(pageNumber * pageSize)
.Take(pageSize)
.ToListAsync<IMusicGroup>()
```

The total count is computed in the same query using a separate `.CountAsync()` and placed in `DbItemsCount` on the response DTO so the client knows the total without a second request.

**`navProp_*` private methods for navigation-property resolution**  
When creating or updating, the DTO carries Guid IDs for related entities. A private helper method resolves each Guid to a tracked `DbM` instance and assigns it to the navigation property. If a Guid does not match any row an `ArgumentException` is thrown:

```csharp
// MusicGroupsDbRepos
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

// AlbumsDbRepos
private async Task navProp_AlbumCUdto_to_AlbumDbM(
    AlbumCUdto itemDtoSrc, AlbumDbM itemDst)
{
    var musicGroup = await _dbContext.MusicGroups
        .FirstOrDefaultAsync(a => a.MusicGroupId == itemDtoSrc.MusicGroupId);
    if (musicGroup == null)
        throw new ArgumentException($"Item id {itemDtoSrc.MusicGroupId} not existing");
    itemDst.MusicGroupDbM = musicGroup;
}
```

**Update reads back after saving**  
Both `UpdateAsync` and `CreateAsync` return a non-flat read of the saved item so the response always reflects the fully populated state:

```csharp
await _dbContext.SaveChangesAsync();
return await ReadMusicGroupAsync(item.MusicGroupId, false);
```

**Delete does not load navigation properties**  
The delete path only fetches the item by primary key (no `.Include()`), removes it, and saves. Cascade delete in the database handles child rows:

```csharp
var item = await query1.FirstOrDefaultAsync<MusicGroupDbM>();
if (item == null) throw new ArgumentException($"Item {id} is not existing");
_dbContext.MusicGroups.Remove(item);
await _dbContext.SaveChangesAsync();
```

---

## 4. New service interfaces and implementations

### `Services/Interfaces.cs`

Two new interfaces are added alongside the existing `IAdminService`:

```csharp
public interface IMusicGroupsService
{
    Task<ResponsePageDto<IMusicGroup>> ReadMusicGroupsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize);
    Task<ResponseItemDto<IMusicGroup>> ReadMusicGroupAsync(Guid id, bool flat);
    Task<ResponseItemDto<IMusicGroup>> DeleteMusicGroupAsync(Guid id);
    Task<ResponseItemDto<IMusicGroup>> UpdateMusicGroupAsync(MusicGroupCUdto item);
    Task<ResponseItemDto<IMusicGroup>> CreateMusicGroupAsync(MusicGroupCUdto item);
}

public interface IAlbumsService
{
    Task<ResponsePageDto<IAlbum>> ReadAlbumsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize);
    Task<ResponseItemDto<IAlbum>> ReadAlbumAsync(Guid id, bool flat);
    Task<ResponseItemDto<IAlbum>> DeleteAlbumAsync(Guid id);
    Task<ResponseItemDto<IAlbum>> UpdateAlbumAsync(AlbumCUdto item);
    Task<ResponseItemDto<IAlbum>> CreateAlbumAsync(AlbumCUdto item);
}
```

Return types use the **interface** (`IMusicGroup`, `IAlbum`) not the concrete `DbM` type — the controller layer never sees EF Core models.

### `Services/MusicGroupsServiceDb.cs` and `Services/AlbumsServiceDb.cs`

Both service implementations are thin pass-throughs; business logic can be added here in later branches without touching the controller or repo:

```csharp
public class MusicGroupsServiceDb : IMusicGroupsService
{
    private readonly MusicGroupsDbRepos _repo;

    public Task<ResponsePageDto<IMusicGroup>> ReadMusicGroupsAsync(...) => _repo.ReadMusicGroupsAsync(...);
    public Task<ResponseItemDto<IMusicGroup>> ReadMusicGroupAsync(...)  => _repo.ReadMusicGroupAsync(...);
    public Task<ResponseItemDto<IMusicGroup>> DeleteMusicGroupAsync(...) => _repo.DeleteMusicGroupAsync(...);
    public Task<ResponseItemDto<IMusicGroup>> UpdateMusicGroupAsync(...) => _repo.UpdateMusicGroupAsync(...);
    public Task<ResponseItemDto<IMusicGroup>> CreateMusicGroupAsync(...) => _repo.CreateMusicGroupAsync(...);
}
```

---

## 5. Two new controllers

`MusicGroupsController` and `AlbumsController` follow the same six-endpoint pattern.

| Endpoint | HTTP verb | Route | Description |
|---|---|---|---|
| `Read` | `GET` | `api/MusicGroups/Read` | Paged + filtered list |
| `ReadItem` | `GET` | `api/MusicGroups/ReadItem` | Single item by Id (flat or full) |
| `ReadItemDto` | `GET` | `api/MusicGroups/ReadItemDto` | Single item projected to CU DTO |
| `DeleteItem` | `DELETE` | `api/MusicGroups/DeleteItem/{id}` | Delete by Id |
| `UpdateItem` | `PUT` | `api/MusicGroups/UpdateItem/{id}` | Update from request body DTO |
| `CreateItem` | `POST` | `api/MusicGroups/CreateItem` | Create from request body DTO |

### `Read` — paged list with input validation

All string query parameters are parsed to their target types inside `try/catch`. The `filter` string is validated with a regex before it is passed to the repo — rejecting anything that is not alphanumeric or a space prevents the filter being used for injection:

```csharp
if (!string.IsNullOrEmpty(filter) && !Regex.IsMatch(filter, @"^[a-zA-Z0-9\s]*$"))
    throw new ArgumentException("Filter can only contain letters (a-z), numbers (0-9), and spaces.");
```

### `ReadItemDto` — projects the domain model to a CU DTO

Reads the full item from the service, then wraps it in a `ResponseItemDto<MusicGroupCUdto>` by constructing the DTO from the model. This is the endpoint the client calls before an edit form to pre-populate the form fields:

```csharp
var item = await _service.ReadMusicGroupAsync(idArg, false);
return Ok(new ResponseItemDto<MusicGroupCUdto>()
{
#if DEBUG
    ConnectionString = item.ConnectionString,
#endif
    Item = new MusicGroupCUdto(item.Item)
});
```

### `UpdateItem` — Id mismatch guard

The route `{id}` and the DTO body both carry the Id. The controller verifies they match before calling the service:

```csharp
if (item.MusicGroupId != idArg)
    throw new ArgumentException("Id mismatch");
```

### `CreateItem` — null Id enforced

The repo enforces that `MusicGroupId` is `null` on creation (a new Guid is assigned in the constructor). The controller does not need to strip it — the repo throws if it is already set:

```csharp
if (itemDto.MusicGroupId != null)
    throw new ArgumentException($"{nameof(itemDto.MusicGroupId)} must be null when creating a new object");
```

---

## 6. `AppWebApi/Program.cs` — new registrations

Four lines are added to wire up the new repos and services:

```csharp
// Before (13-schema-annotations)
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<IAdminService, AdminServiceDb>();

// After (18-crud-complete)
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<MusicGroupsDbRepos>();
builder.Services.AddScoped<AlbumsDbRepos>();

builder.Services.AddScoped<IAdminService, AdminServiceDb>();
builder.Services.AddScoped<IMusicGroupsService, MusicGroupsServiceDb>();
builder.Services.AddScoped<IAlbumsService, AlbumsServiceDb>();
```

All repos and services are `Scoped` — they hold a reference to `MainDbContext` which is also `Scoped`, so lifetimes match.

---

## Summary of key concepts introduced

| Concept | Where |
|---|---|
| `ResponsePageDto<T>` / `ResponseItemDto<T>` — typed API response wrappers | `Models/DTO/ResponseDto.cs` |
| CU (Create/Update) DTO — Guid lists instead of nested objects for relationships | `Models/DTO/CuDto.cs` |
| DTO constructor + `UpdateFromDTO()` on DbM classes | `MusicGroupDbM`, `AlbumDbM` |
| `AsNoTracking()` on all read queries | `MusicGroupsDbRepos`, `AlbumsDbRepos` |
| `flat` parameter — controls `.Include()` / eager loading per call | Both repos, both controllers |
| `seeded` filter — separates seed data from user-created data | Both repos, both controllers |
| `.Skip().Take()` pagination with total count in same response | Both repos |
| `navProp_*` pattern — resolves Guid IDs from DTOs to tracked navigation properties | Both repos |
| Regex input validation on `filter` string | Both controllers |
| `ReadItemDto` endpoint — read an item pre-projected to a CU DTO for edit forms | Both controllers |
| Id-mismatch guard on `UpdateItem` | Both controllers |
| Null-Id guard on `CreateItem` | Both repos |
