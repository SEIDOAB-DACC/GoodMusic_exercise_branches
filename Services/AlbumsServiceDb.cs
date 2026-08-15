using Microsoft.Extensions.Logging;

using DbRepos;
using Models.Interfaces;
using Models.DTO;

namespace Services;

public class AlbumsServiceDb : IAlbumsService
{
    private readonly AlbumsDbRepos _repo;
    private readonly ILogger<AlbumsServiceDb> _logger;

    #region constructors
    public AlbumsServiceDb(AlbumsDbRepos repo)
    {
        _repo = repo;
    }
    public AlbumsServiceDb(AlbumsDbRepos repo, ILogger<AlbumsServiceDb> logger):this(repo)
    {
        _logger = logger;
    }
    #endregion

    public Task<ResponsePageDto<IAlbum>> ReadAlbumsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize) => _repo.ReadAlbumsAsync(seeded, flat, filter, pageNumber, pageSize);
    public Task<ResponseItemDto<IAlbum>> ReadAlbumAsync(Guid id, bool flat) => _repo.ReadAlbumAsync(id, flat);
    public Task<ResponseItemDto<IAlbum>> DeleteAlbumAsync(Guid id) => _repo.DeleteAlbumAsync(id);
    public Task<ResponseItemDto<IAlbum>> UpdateAlbumAsync(AlbumCUdto item) => _repo.UpdateAlbumAsync(item);
    public Task<ResponseItemDto<IAlbum>> CreateAlbumAsync(AlbumCUdto item) => _repo.CreateAlbumAsync(item);
}

