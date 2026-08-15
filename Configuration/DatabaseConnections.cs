using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Configuration.Options;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace Configuration;

public enum DatabaseServer { SQLServer, MySql, PostgreSql, SQLite }
public class DatabaseConnections
{
    readonly IConfiguration _configuration;
    readonly DbConnectionSetsOptions _options;
    private readonly DbSetDetailOptions _activeDataSet;

    public SetupInformation SetupInfo => new SetupInformation()
    {
        SecretSource = _configuration.GetValue<string>("ApplicationSecrets:SecretStorage"),

        DefaultDataUser = _configuration["DatabaseConnections:DefaultDataUser"],
        MigrationUser = _configuration["DatabaseConnections:MigrationUser"],
        
        DataConnectionTag = _activeDataSet.DbTag,
        DataConnectionServer = _activeDataSet.DbServer.Trim().ToLower() switch
        {
            "sqlserver" => DatabaseServer.SQLServer,
            "mysql" => DatabaseServer.MySql,
            "postgresql" => DatabaseServer.PostgreSql,
            "sqlite" => DatabaseServer.SQLite,
            _ => throw new NotSupportedException($"DbServer {_activeDataSet.DbServer} not supported")
        },

    };

    public DbConnectionDetailOptions GetDataConnectionDetails(string user) => GetLoginDetails(user, _activeDataSet);

    DbConnectionDetailOptions GetLoginDetails(string user, DbSetDetailOptions dataSet)
    {
        if (string.IsNullOrEmpty(user) || string.IsNullOrWhiteSpace(user))
            throw new ArgumentNullException(nameof(user));

        var conn = dataSet.DbConnections.First(m => m.DbUserLogin.Trim().ToLower() == user.Trim().ToLower());
        return new DbConnectionDetailOptions
        {
            DbUserLogin = conn.DbUserLogin,
            DbConnection = conn.DbConnection,
            DbConnectionString = _configuration.GetConnectionString(conn.DbConnection)
        };
    }

    public DatabaseConnections(IConfiguration configuration, IOptions<DbConnectionSetsOptions> dbSetOption)
    {
        _configuration = configuration;
        _options = dbSetOption.Value;

        _activeDataSet = _options.DataSets.FirstOrDefault(ds => ds.DbTag.Trim().ToLower() == configuration["DatabaseConnections:UseDataSetWithTag"].Trim().ToLower());
        if (_activeDataSet == null)
            throw new ArgumentException($"Dataset with DbTag {configuration["DatabaseConnections:UseDataSetWithTag"]} not found");
    }


    public class SetupInformation
    {
        private string _userSecretsId = null;
        public string SecretSource { get; init; }
        public string SecretId => SecretSource switch
        {
            "AzureKeyVault" => $"{Environment.GetEnvironmentVariable("AzureKeyVault_kvAccessParams_kvSecret")}",
            _ => _userSecretsId
        };

        public string AppEnvironment => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        public string DataConnectionTag {get; init;}
        public string DefaultDataUser {get; init;}
        public string MigrationUser {get; init;}
        public DatabaseServer DataConnectionServer {get; init;}
        public string DataConnectionServerString => DataConnectionServer.ToString();  //for json clear text

        public SetupInformation()
        {
            var assembly = System.Reflection.Assembly.Load("Configuration");
            var userSecretsIdAttribute = assembly.GetCustomAttributes(typeof(UserSecretsIdAttribute), false)
                .FirstOrDefault() as UserSecretsIdAttribute;
            _userSecretsId = userSecretsIdAttribute?.UserSecretsId;
        }
    }
}