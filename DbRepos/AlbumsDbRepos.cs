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
            //make sure the model is fully populated, try without include.
            //remove tracking for all read operations for performance and to avoid recursion/circular access
            var query = _dbContext.Albums.AsNoTracking()
                .Include(i => i.MusicGroupDbM)
                .Where(i => i.AlbumId == id);

            item = await query.FirstOrDefaultAsync<IAlbum>();

        }
        else
        {
            //Not fully populated, compare the SQL Statements generated
            //remove tracking for all read operations for performance and to avoid recursion/circular access
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

    public async Task<ResponsePageDto<IAlbum>> ReadAlbumsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize)
      {
        filter ??= "";
        IQueryable<AlbumDbM> query;
        if (flat)
        {
            query = _dbContext.Albums.AsNoTracking();
        }
        else
        {
            query = _dbContext.Albums.AsNoTracking()
                .Include(i => i.MusicGroupDbM);
        }

        var ret = new ResponsePageDto<IAlbum>()
        {
#if DEBUG
            ConnectionString = _dbContext.dbConnection,
#endif
            DbItemsCount = await query

            //Adding filter functionality
            .Where(i => (i.Seeded == seeded) && 
                        (i.Name.ToLower().Contains(filter))).CountAsync(),

            PageItems = await query

            //Adding filter functionality
            .Where(i => (i.Seeded == seeded) && 
                        (i.Name.ToLower().Contains(filter)))

            //Adding paging
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
        var query1 = _dbContext.Albums
            .Where(i => i.AlbumId == id);

        var item = await query1.FirstOrDefaultAsync<AlbumDbM>();

        //If the item does not exists
        if (item == null) throw new ArgumentException($"Item {id} is not existing");

        //delete in the database model
        _dbContext.Albums.Remove(item);

        //write to database in a UoW
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
       //Find the instance with matching id and read the navigation properties.
       var query1 = _dbContext.Albums
            .Where(i => i.AlbumId == itemDto.AlbumId);
        var item = await query1
                .Include(i => i.MusicGroupDbM)
                .FirstOrDefaultAsync<AlbumDbM>();

        //If the item does not exists
        if (item == null) throw new ArgumentException($"Item {itemDto.AlbumId} is not existing");

        //transfer any changes from DTO to database objects
        //Update individual properties 
        item.UpdateFromDTO(itemDto);

        //Update navigation properties
        await navProp_AlbumCUdto_to_AlbumDbM(itemDto, item);

        //write to database model
        _dbContext.Albums.Update(item);

        //write to database in a UoW
        await _dbContext.SaveChangesAsync();
        
        //return the updated item in non-flat mode
        return await ReadAlbumAsync(item.AlbumId, false);    
    }

    public async Task<ResponseItemDto<IAlbum>> CreateAlbumAsync(AlbumCUdto itemDto)
    { 
        if (itemDto.AlbumId != null)
            throw new ArgumentException($"{nameof(itemDto.AlbumId)} must be null when creating a new object");

        //transfer any changes from DTO to database objects
        //Update individual properties. Seeded always false on created items
        itemDto.Seeded = false; 
        var item = new AlbumDbM(itemDto);

        //Update navigation properties
        await navProp_AlbumCUdto_to_AlbumDbM(itemDto, item);

        //write to database model
        _dbContext.Albums.Add(item);

        //write to database in a UoW
        await _dbContext.SaveChangesAsync();
        
        //return the updated item in non-flat mode
        return await ReadAlbumAsync(item.AlbumId, false);    
    }

    //from all Guid relationships in _itemDtoSrc finds the corresponding object in the database and assigns it to _itemDst 
    //as navigation properties. Error is thrown if no object is found corresponing to an id.
    private async Task navProp_AlbumCUdto_to_AlbumDbM(AlbumCUdto itemDtoSrc, AlbumDbM itemDst)
    {
        //Navigation prop Albums
        var musicGroup = await _dbContext.MusicGroups.FirstOrDefaultAsync(a => a.MusicGroupId == itemDtoSrc.MusicGroupId);
        if (musicGroup == null)
            throw new ArgumentException($"Item id {itemDtoSrc.MusicGroupId} not existing");
        
        itemDst.MusicGroupDbM = musicGroup;
    }
 }
