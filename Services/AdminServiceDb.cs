using Microsoft.Extensions.Logging;

using DbRepos;
using Models.DTO;

namespace Services;
    
public class AdminServiceDb : IAdminService
{
    private readonly AdminDbRepos _repo = null;
    private readonly ILogger<AdminServiceDb> _logger = null;

    public Task SeedAsync(int seedCount) => _repo.SeedAsync(seedCount);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded) => _repo.RemoveSeedAsync(seeded);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> GuestInfoAsync() => _repo.InfoAsync();
    public Task<ResponseItemDto<UsrInfoDto>> SeedUsersAsync(int nrOfUsers, int nrOfSuperUsers, int nrOfSysAdmin) => _repo.SeedUsersAsync(nrOfUsers, nrOfSuperUsers, nrOfSysAdmin);

    #region constructors
    public AdminServiceDb(AdminDbRepos repo)
    {
        _repo = repo;
    }
    public AdminServiceDb(AdminDbRepos repo, ILogger<AdminServiceDb> logger):this(repo)
    {
        _logger = logger;
    }
    #endregion
}

