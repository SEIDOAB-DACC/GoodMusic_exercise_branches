using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using MySqlConnector;
using Microsoft.Data.SqlClient;
using Npgsql;


using Models.DTO;
using DbContext;
using Configuration;

namespace DbRepos;
public class LoginDbRepos
{
    private readonly ILogger<LoginDbRepos> _logger;
    private readonly MainDbContext _dbContext;
    private Encryptions _encryptions;

    public LoginDbRepos(ILogger<LoginDbRepos> logger, Encryptions encryptions, MainDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
        _encryptions = encryptions;
    }

     public async Task<ResponseItemDto<LoginUserSessionDto>> LoginUserAsync(LoginCredentialsDto usrCreds)
    {
        using (var cmd1 = _dbContext.Database.GetDbConnection().CreateCommand())
        {
            //Notice how I use the efc Command to call sp as I do not return any dataset, only output parameters
            //Notice also how I encrypt the password, no coms to database with open password
            cmd1.CommandType = CommandType.StoredProcedure;
            
            // Create parameters based on database provider
            DbParameter userNameParam, userPasswordParam, userIdParam, userNameOutParam, userRoleParam;
            var connection = _dbContext.Database.GetDbConnection();
            
            if (connection is MySqlConnection)
            {
                cmd1.CommandText = "gstusr_spLogin";

                // MySQL parameters
                userNameParam = new MySqlParameter("UserNameOrEmail", usrCreds.UserNameOrEmail);
                userPasswordParam = new MySqlParameter("UserPassword", _encryptions.EncryptPasswordToBase64(usrCreds.UserPassword));
                userIdParam = new MySqlParameter("UserId", MySqlDbType.Guid) { Direction = ParameterDirection.Output };
                userNameOutParam = new MySqlParameter("UserName", MySqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                userRoleParam = new MySqlParameter("UserRole", MySqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
            }
            else if (connection is NpgsqlConnection)
            {
                cmd1.CommandText = "SELECT userid, username, userrole FROM gstusr.\"spLogin\"(@usernameoremail, @userpasswordhash)";
                cmd1.CommandType = CommandType.Text;

                // PostgreSQL parameters
                userNameParam = new NpgsqlParameter("usernameoremail", usrCreds.UserNameOrEmail);
                userPasswordParam = new NpgsqlParameter("userpasswordhash", _encryptions.EncryptPasswordToBase64(usrCreds.UserPassword));
                userIdParam = new NpgsqlParameter("userid", NpgsqlTypes.NpgsqlDbType.Uuid) { Direction = ParameterDirection.Output };
                userNameOutParam = new NpgsqlParameter("username", NpgsqlTypes.NpgsqlDbType.Varchar, 100) { Direction = ParameterDirection.Output };
                userRoleParam = new NpgsqlParameter("userrole", NpgsqlTypes.NpgsqlDbType.Varchar, 100) { Direction = ParameterDirection.Output };
            }
            else
            {
                cmd1.CommandText = "gstusr.spLogin";

                // SQL Server parameters (default)
                userNameParam = new SqlParameter("UserNameOrEmail", usrCreds.UserNameOrEmail);
                userPasswordParam = new SqlParameter("UserPassword", _encryptions.EncryptPasswordToBase64(usrCreds.UserPassword));
                userIdParam = new SqlParameter("UserId", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.Output };
                userNameOutParam = new SqlParameter("UserName", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                userRoleParam = new SqlParameter("UserRole", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
            }

            cmd1.Parameters.Add(userNameParam);
            cmd1.Parameters.Add(userPasswordParam);
            int _usrIdIdx = cmd1.Parameters.Add(userIdParam);
            int _usrIdx = cmd1.Parameters.Add(userNameOutParam);
            int _roleIdx = cmd1.Parameters.Add(userRoleParam);

            if (connection.State != ConnectionState.Open)
                await _dbContext.Database.OpenConnectionAsync();
            await cmd1.ExecuteScalarAsync();

            var info = new LoginUserSessionDto
            {
                //Notice the soft cast conversion 'as' it will be null if cast cannot be made
                UserId = cmd1.Parameters[_usrIdIdx].Value as Guid?,
                UserName = cmd1.Parameters[_usrIdx].Value as string,
                UserRole = cmd1.Parameters[_roleIdx].Value as string
            };

            return new ResponseItemDto<LoginUserSessionDto>()
            {
#if DEBUG
                ConnectionString = _dbContext.dbConnection,
#endif
                Item = info
            };
        }
    }
}


