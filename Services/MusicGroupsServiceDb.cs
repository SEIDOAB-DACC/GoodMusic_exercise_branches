using Microsoft.Extensions.Logging;

using DbRepos;
using Models.Interfaces;
using Models.DTO;

namespace Services;

public class MusicGroupsServiceDb : IMusicGroupsService
{
    private readonly MusicGroupsDbRepos _repo;
    private readonly ILogger<MusicGroupsServiceDb> _logger;

    #region constructors
    public MusicGroupsServiceDb(MusicGroupsDbRepos repo)
    {
        _repo = repo;
    }
    public MusicGroupsServiceDb(MusicGroupsDbRepos repo, ILogger<MusicGroupsServiceDb> logger):this(repo)
    {
        _logger = logger;
    }
    #endregion

    public Task<ResponsePageDto<IMusicGroup>> ReadMusicGroupsAsync(bool seeded, bool flat, string filter, int pageNumber, int pageSize) => _repo.ReadMusicGroupsAsync(seeded, flat, filter, pageNumber, pageSize);
    public Task<ResponseItemDto<IMusicGroup>> ReadMusicGroupAsync(Guid id, bool flat) => _repo.ReadMusicGroupAsync(id, flat);
    public Task<ResponseItemDto<IMusicGroup>> DeleteMusicGroupAsync(Guid id) => _repo.DeleteMusicGroupAsync(id);
    public Task<ResponseItemDto<IMusicGroup>> UpdateMusicGroupAsync(MusicGroupCUdto item) => _repo.UpdateMusicGroupAsync(item);
    public Task<ResponseItemDto<IMusicGroup>> CreateMusicGroupAsync(MusicGroupCUdto item) => _repo.CreateMusicGroupAsync(item);
}

