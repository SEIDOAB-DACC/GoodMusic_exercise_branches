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
            //make sure the model is fully populated, try without include.
            //remove tracking for all read operations for performance and to avoid recursion/circular access
            var query = _dbContext.MusicGroups.AsNoTracking()
                .Include(i => i.AlbumsDbM)
                .Where(i => i.MusicGroupId == id);

            item = await query.FirstOrDefaultAsync<IMusicGroup>();
        }
        else
        {
            //Not fully populated, compare the SQL Statements generated
            //remove tracking for all read operations for performance and to avoid recursion/circular access
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


    public async Task<ResponsePageDto<IMusicGroup>> ReadMusicGroupsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize)
    {
        filter ??= "";
        IQueryable<MusicGroupDbM> query;
        if (flat)
        {
            query = _dbContext.MusicGroups.AsNoTracking();
        }
        else
        {
            query = _dbContext.MusicGroups.AsNoTracking()
                .Include(i => i.AlbumsDbM);
        }

        var ret = new ResponsePageDto<IMusicGroup>()
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

            .ToListAsync<IMusicGroup>(),

            PageNr = pageNumber,
            PageSize = pageSize
        };
        return ret;
    }


    public async Task<ResponseItemDto<IMusicGroup>> DeleteMusicGroupAsync(Guid id)
    {
        //Find the instance with matching id
        var query1 = _dbContext.MusicGroups
            .Where(i => i.MusicGroupId == id);
        var item = await query1.FirstOrDefaultAsync<MusicGroupDbM>();

        //If the item does not exists
        if (item == null) throw new ArgumentException($"Item {id} is not existing");

        //delete in the database model
        _dbContext.MusicGroups.Remove(item);

        //write to database in a UoW
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
        //Find the instance with matching id and read the navigation properties.
        var query1 = _dbContext.MusicGroups
            .Where(i => i.MusicGroupId == itemDto.MusicGroupId);
        var item = await query1
            .Include(i => i.AlbumsDbM)
            .FirstOrDefaultAsync<MusicGroupDbM>();

        //If the item does not exists
        if (item == null) throw new ArgumentException($"Item {itemDto.MusicGroupId} is not existing");

        //transfer any changes from DTO to database objects
        //Update individual properties
        item.UpdateFromDTO(itemDto);

        //Update navigation properties
        await navProp_MusicGroupCUdto_To_MusicGroup(itemDto, item);

        //write to database model
        _dbContext.MusicGroups.Update(item);

        //write to database in a UoW
        await _dbContext.SaveChangesAsync();

        //return the updated item in non-flat mode
        return await ReadMusicGroupAsync(item.MusicGroupId, false);    
    }

    public async Task<ResponseItemDto<IMusicGroup>> CreateMusicGroupAsync(MusicGroupCUdto itemDto)
    {
        if (itemDto.MusicGroupId != null)
            throw new ArgumentException($"{nameof(itemDto.MusicGroupId)} must be null when creating a new object");

        //transfer any changes from DTO to database objects
        //Update individual properties. Seeded always false on created items
        itemDto.Seeded = false; 
        var item = new MusicGroupDbM(itemDto);

        //Update navigation properties
        await navProp_MusicGroupCUdto_To_MusicGroup(itemDto, item);

        //write to database model
        _dbContext.MusicGroups.Add(item);

        //write to database in a UoW
        await _dbContext.SaveChangesAsync();
        
        //return the updated item in non-flat mode
        return await ReadMusicGroupAsync(item.MusicGroupId, false);   
    }

    //from all Guid relationships in _itemDtoSrc finds the corresponding object in the database and assigns it to _itemDst 
    //as navigation properties. Error is thrown if no object is found corresponing to an id.
    private async Task navProp_MusicGroupCUdto_To_MusicGroup(MusicGroupCUdto itemDtoSrc, MusicGroupDbM itemDst)
    {
        //Navigation prop Albums
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
