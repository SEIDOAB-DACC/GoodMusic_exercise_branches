using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;


using Seido.Utilities.SeedGenerator;
using DbModels;
using DbContext;
using Configuration;
using Models.DTO;

namespace DbRepos;

public class AdminDbRepos
{
    private const string _seedSource = "./app-seeds.json";
    private readonly ILogger<AdminDbRepos> _logger;
    private Encryptions _encryptions;
    private readonly MainDbContext _dbContext;

    public async Task SeedAsync(int seedCount)
    {
        //Create a seeder
        var fn = Path.GetFullPath(_seedSource);
        var seeder = new SeedGenerator(fn);

        //Seeding new music groups into the database
        var musicGroups = seeder.ItemsToList<MusicGroupDbM>(seedCount);

        //Set between 5 and 50 albums for each music groups
        musicGroups.ForEach(mg => mg.AlbumsDbM = seeder.ItemsToList<AlbumDbM>(seeder.Next(2, 5)));

        _dbContext.MusicGroups.AddRange(musicGroups);

        //Save changes to the database
        await _dbContext.SaveChangesAsync();
    }

    public async Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded)
    {
        // Create parameters based on database provider
        var connection = _dbContext.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;

        // SQL Server parameters (default)
        command.CommandText = "supusr.spDeleteAll";
        List<DbParameter> parameters = new List<DbParameter>
        {
            new SqlParameter("seededParam", seeded),
            new SqlParameter("nrMusicGroupsAffected", SqlDbType.Int) { Direction = ParameterDirection.Output },
            new SqlParameter("nrAlbumsAffected", SqlDbType.Int) { Direction = ParameterDirection.Output }
        };

        command.Parameters.AddRange(parameters.ToArray());

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        // Execute the stored procedure and get the result set
        using var reader = await command.ExecuteReaderAsync();

        // map reader result into GstUsrInfoDbDto result_set
        GstUsrInfoDbDto result_set = null;
        if (reader.HasRows)
        {
            // Read the first result set which should be InfoDbView
            await reader.ReadAsync();

            result_set = new GstUsrInfoDbDto
            {
                // Populate properties from the reader
                NrSeededMusicGroups = Convert.ToInt32(reader["NrSeededMusicGroups"]),
                NrUnseededMusicGroups = Convert.ToInt32(reader["NrUnseededMusicGroups"]),
                NrSeededAlbums = Convert.ToInt32(reader["NrSeededAlbums"]),
                NrUnseededAlbums = Convert.ToInt32(reader["NrUnseededAlbums"])
            };

            await reader.CloseAsync();
            // result_set can now be accessed - not used in this example
        }

        return await DbInfo();
    }

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

    public AdminDbRepos(ILogger<AdminDbRepos> logger, Encryptions encryptions, MainDbContext context)
    {
        _logger = logger;
        _encryptions = encryptions;
        _dbContext = context;
    }
}
