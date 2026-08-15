# Step-by-step: from `20-database-objects` to `22-jwt-security`

This branch wires up a full JWT authentication and role-based authorisation stack. The central idea: after a successful login the client receives a JWT token containing a `UserRole` claim; every subsequent request presents that token and the application opens the database connection under the matching database user, so the database itself enforces what the caller can read or write.

Work through the steps in order.

---

## Step 1 — Add NuGet packages to `Configuration/Configuration.csproj`

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.10" />
<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" Version="8.22.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.22.0" />
```

---

## Step 2 — Create `Configuration/JwtToken.cs`

The return type of token creation. In debug builds the decoded claims are included so they can be inspected directly in the API response:

```csharp
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Configuration.Options;

namespace Configuration;

public class JwtToken
{
    public string EncryptedToken { get; set; }

#if DEBUG
    public Guid TokenId { get; set; }
    public DateTime ExpireTime { get; set; }
    public IDictionary<string, string> UserClaims { get; set; }
#endif
}
```

---

## Step 3 — Create `Configuration/JwtEncryptions.cs`

Creates HMAC-SHA256 signed tokens and decodes them. The signing key and expiry come from `JwtOptions` (already in user secrets from earlier branches):

```csharp
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Configuration.Options;

namespace Configuration;

public class JwtEncryptions
{
    private readonly JwtOptions _jwtOptions;

    public JwtEncryptions(IOptions<JwtOptions> jwtOptions)
        => _jwtOptions = jwtOptions.Value;

    private IEnumerable<Claim> CreateClaims(Guid TokenId, string Role, IDictionary<string, string> userClaims)
    {
        IEnumerable<Claim> claims = new List<Claim>();
        foreach (var kvp in userClaims)
            claims = claims.Append(new Claim(kvp.Key, kvp.Value));

        claims = claims.Append(new Claim(ClaimTypes.Expiration,
            DateTime.UtcNow.AddMinutes(_jwtOptions.LifeTimeMinutes).ToString("MMM ddd dd yyyy HH:mm:ss tt")));
        claims = claims.Append(new Claim(ClaimTypes.NameIdentifier, TokenId.ToString()));
        claims = claims.Append(new Claim(ClaimTypes.Role, Role));
        return claims;
    }

    public JwtToken CreateToken(string Role, IDictionary<string, string> userClaims)
    {
        Guid tokenId = Guid.NewGuid();
        var encryptionKey = System.Text.Encoding.ASCII.GetBytes(_jwtOptions.IssuerSigningKey);
        DateTime expireTime = DateTime.UtcNow.AddMinutes(_jwtOptions.LifeTimeMinutes);

        var JWToken = new JwtSecurityToken(
            issuer: _jwtOptions.ValidIssuer,
            audience: _jwtOptions.ValidAudience,
            claims: CreateClaims(tokenId, Role, userClaims),
            notBefore: new DateTimeOffset(DateTime.UtcNow).DateTime,
            expires: new DateTimeOffset(expireTime).DateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(encryptionKey), SecurityAlgorithms.HmacSha256));

        var token = new JwtToken
        {
            EncryptedToken = new JwtSecurityTokenHandler().WriteToken(JWToken)
        };
#if DEBUG
        token.UserClaims = userClaims;
        token.TokenId = tokenId;
        token.ExpireTime = expireTime;
#endif
        return token;
    }

    public IDictionary<string, string> GetClaimsFromToken(string encryptedToken)
    {
        if (encryptedToken == null) return null;
        return new JwtSecurityTokenHandler()
            .ReadJwtToken(encryptedToken)?.Claims
            ?.ToDictionary(c => c.Type, c => c.Value);
    }
}
```

---

## Step 4 — Create `Configuration/Extensions/JWTExtensions.cs`

`AddJwtToken()` registers the JWT bearer scheme and `JwtEncryptions` in one call. `SaveToken = true` stores the raw token in the HTTP context so `DbContextExtensions` can read it per-request:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Configuration.Options;

namespace Configuration.Extensions;

public static class JWTEncryptionsExtensions
{
    public static void AddJwtToken(this IServiceCollection Services, IConfiguration configuration)
    {
        Services.AddAuthentication(options => {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options => {
            var jwtOptions = configuration.GetSection(JwtOptions.Position).Get<JwtOptions>();
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;   // stores token in HttpContext for retrieval via GetTokenAsync
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = jwtOptions.ValidateIssuerSigningKey,
                IssuerSigningKey         = new SymmetricSecurityKey(
                                               System.Text.Encoding.UTF8.GetBytes(jwtOptions.IssuerSigningKey)),
                ValidateIssuer           = jwtOptions.ValidateIssuer,
                ValidIssuer              = jwtOptions.ValidIssuer,
                ValidateAudience         = jwtOptions.ValidateAudience,
                ValidAudience            = jwtOptions.ValidAudience,
                RequireExpirationTime    = jwtOptions.RequireExpirationTime,
                ValidateLifetime         = jwtOptions.RequireExpirationTime,
                ClockSkew                = TimeSpan.FromDays(1),
            };
        });

        Services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Position));
        Services.AddTransient<JwtEncryptions>();
    }
}
```

---

## Step 5 — Add `IUser` to `Models/Interfaces.cs` and create `Models/User.cs`

Add the interface to the bottom of `Models/Interfaces.cs`:

```csharp
public interface IUser
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string UserRole { get; set; }
}
```

Create `Models/User.cs`:

```csharp
using Models.Interfaces;

namespace Models;

public class User : IUser
{
    public virtual Guid UserId       { get; set; }
    public virtual string UserName   { get; set; }
    public virtual string Email      { get; set; }
    public virtual string PasswordHash { get; set; }
    public virtual string UserRole   { get; set; }
}
```

---

## Step 6 — Create `Models/DTO/LoginDto.cs`

```csharp
using Configuration;

namespace Models.DTO;

public class LoginCredentialsDto
{
    public string UserNameOrEmail { get; set; }
    public string UserPassword    { get; set; }    // plain text — hashed before any DB call
}

public class LoginUserSessionDto
{
    public Guid?    UserId    { get; set; }
    public string   UserName  { get; set; }
    public string   UserRole  { get; set; }
    public JwtToken JwtToken  { get; set; }    // populated by service layer after login
}
```

---

## Step 7 — Create `Models/DTO/UsrDto.cs`

```csharp
namespace Models.DTO;

public class UsrInfoDto
{
    public int NrUsers      { get; set; }
    public int NrSuperUsers { get; set; }
    public int NrDbOwners   { get; set; }
}
```

---

## Step 8 — Create `DbModels/UserDbM.cs`

Stored in the `dbo` schema so all database users can read it (the login procedure runs as `gstusr` and queries `dbo.Users`). `UserRole` holds the string that both identifies the app role and maps to the database connection user:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Models;

namespace DbModels
{
    [Table("Users", Schema = "dbo")]
    public class UserDbM : User
    {
        [Key]
        public override Guid UserId { get; set; }

        [Required]
        public override string UserName { get; set; }

        [Required]
        public override string PasswordHash { get; set; }

        [Required]
        public override string UserRole { get; set; }
    }
}
```

---

## Step 9 — Update `DbContext/MainDbContext.cs`

Add `using Models.DTO;` if not already present, then add the `DbSet` alongside the other tables:

```csharp
    #region C# model of database tables
    public DbSet<MusicGroupDbM> MusicGroups { get; set; }
    public DbSet<AlbumDbM> Albums { get; set; }
    public DbSet<UserDbM> Users { get; set; }        // ← add this line
    #endregion
```

---

## Step 10 — Update `DbContext/Extensions/DbContextExtensions.cs`

This is the architectural centrepiece. Add `AddHttpContextAccessor()` and extend the lambda to read the JWT from the current HTTP request and extract `UserRole` to select the database connection:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Configuration;

namespace DbContext.Extensions;

public static class DbContextExtensions
{
    public static IServiceCollection AddUserBasedDbContext(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();    // required to access the current request in DI
        serviceCollection.AddDbContext<MainDbContext>((serviceProvider, options) =>
        {
            var configuration       = serviceProvider.GetRequiredService<IConfiguration>();
            var databaseConnections = serviceProvider.GetRequiredService<DatabaseConnections>();
            var jwtEncryptions      = serviceProvider.GetRequiredService<JwtEncryptions>();
            var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

            // default: use the role from appsettings.json (unauthenticated requests)
            var userRole = configuration["DatabaseConnections:DefaultDataUser"];

            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var token = httpContext.GetTokenAsync("access_token").Result;
                if (token != null)
                {
                    var claims = jwtEncryptions.GetClaimsFromToken(token);
                    userRole = claims["UserRole"];    // override with the authenticated user's role
                }
            }

            var conn = databaseConnections.GetDataConnectionDetails(userRole);
            if (databaseConnections.SetupInfo.DataConnectionServer == DatabaseServer.SQLServer)
                options.UseSqlServer(conn.DbConnectionString, o => o.EnableRetryOnFailure());
            else if (databaseConnections.SetupInfo.DataConnectionServer == DatabaseServer.MySql)
                options.UseMySql(conn.DbConnectionString, ServerVersion.AutoDetect(conn.DbConnectionString),
                    b => b.SchemaBehavior(Microting.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Translate,
                        (schema, table) => $"{schema}_{table}"));
            else if (databaseConnections.SetupInfo.DataConnectionServer == DatabaseServer.PostgreSql)
                options.UseNpgsql(conn.DbConnectionString);
            else
                throw new InvalidDataException($"DbContext for {databaseConnections.SetupInfo.DataConnectionServer} not existing");
        });

        return serviceCollection;
    }
}
```

---

## Step 11 — Update `AppWebApi/appsettings.json`

Change `DefaultDataUser` from `"root"` to `"gstusr"`. Unauthenticated requests now open the database as the least-privileged user — they can only read the `gstusr` schema:

```json
"DatabaseConnections": {
    "DefaultDataUser": "gstusr",
    "MigrationUser": "root",
    "UseDataSetWithTag": "sql-music.sqlserver.docker"
}
```

---

## Step 12 — Update `Services/Interfaces.cs`

Add `ILoginService` and a new method to `IAdminService`:

```csharp
using Models.Interfaces;
using Models.DTO;

namespace Services;

public interface IAdminService
{
    public Task SeedAsync(int seedCount);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> RemoveSeedAsync(bool seeded);
    public Task<ResponseItemDto<GstUsrInfoAllDto>> GuestInfoAsync();
    public Task<ResponseItemDto<UsrInfoDto>> SeedUsersAsync(int nrOfUsers, int nrOfSuperUsers, int nrOfSysAdmin);
}

public interface ILoginService
{
    public Task<ResponseItemDto<LoginUserSessionDto>> LoginUserAsync(LoginCredentialsDto usrCreds);
}

// IMusicGroupsService and IAlbumsService are unchanged
```

---

## Step 13 — Create `DbRepos/LoginDbRepos.cs`

Calls `gstusr.spLogin` via raw ADO.NET. Provider detected at runtime. The password is hashed before it leaves the application:

```csharp
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
            cmd1.CommandText = "gstusr.spLogin";

            // SQL Server parameters (default)
            userNameParam = new SqlParameter("UserNameOrEmail", usrCreds.UserNameOrEmail);
            userPasswordParam = new SqlParameter("UserPassword", _encryptions.EncryptPasswordToBase64(usrCreds.UserPassword));
            userIdParam = new SqlParameter("UserId", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.Output };
            userNameOutParam = new SqlParameter("UserName", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
            userRoleParam = new SqlParameter("UserRole", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };

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
```

---

## Step 14 — Create `Services/LoginServiceDb.cs`

Takes the repo result, builds the JWT token, and in debug builds immediately verifies the `UserId` claim round-trips correctly:

```csharp
using Microsoft.Extensions.Logging;
using DbRepos;
using Models.DTO;
using Configuration;

namespace Services;

public class LoginServiceDb : ILoginService
{
    private readonly LoginDbRepos _repo;
    private readonly JwtEncryptions _jtwEncryptions;
    private readonly ILogger<LoginServiceDb> _logger;

    public LoginServiceDb(ILogger<LoginServiceDb> logger, LoginDbRepos repo, JwtEncryptions jtwEncryptions)
    {
        _repo = repo;
        _logger = logger;
        _jtwEncryptions = jtwEncryptions;
    }

    public async Task<ResponseItemDto<LoginUserSessionDto>> LoginUserAsync(LoginCredentialsDto usrCreds)
    {
        try
        {
            var _usrSession = await _repo.LoginUserAsync(usrCreds);

            IDictionary<string, string> userClaims = new Dictionary<string, string>
            {
                ["UserId"]   = _usrSession.Item.UserId.ToString(),
                ["UserRole"] = _usrSession.Item.UserRole,
                ["UserName"] = _usrSession.Item.UserName
            };
            _usrSession.Item.JwtToken = _jtwEncryptions.CreateToken(_usrSession.Item.UserRole, userClaims);

#if DEBUG
            // verify token round-trips correctly before returning it
            var claims = _jtwEncryptions.GetClaimsFromToken(_usrSession.Item.JwtToken.EncryptedToken);
            if (claims["UserId"] != _usrSession.Item.UserId.ToString())
                throw new InvalidOperationException("JWT token decryption failed - UserId mismatch");
#endif
            return _usrSession;
        }
        catch
        {
            throw;
        }
    }
}
```

---

## Step 15 — Update `DbRepos/AdminDbRepos.cs`

Add the missing usings at the top:

```csharp
using Models.DTO;
```

Then add `SeedUsersAsync` before the constructor. It deletes all existing users first, then inserts users for each role with their password hashed. The method counts rows by role and returns them in `UsrInfoDto`:

```csharp
public async Task<ResponseItemDto<UsrInfoDto>> SeedUsersAsync(int nrOfUsers, int nrOfSuperUsers, int nrOfDbOwners)
{
    _logger.LogInformation($"Seeding {nrOfUsers} users and {nrOfSuperUsers} superusers");

    foreach (var u in _dbContext.Users)
        _dbContext.Users.Remove(u);

    for (int i = 1; i <= nrOfUsers; i++)
        _dbContext.Users.Add(new UserDbM
        {
            UserId       = Guid.NewGuid(),
            UserName     = $"user{i}",
            Email        = $"user{i}@gmail.com",
            PasswordHash = _encryptions.EncryptPasswordToBase64($"user{i}"),
            UserRole     = "usr"
        });

    for (int i = 1; i <= nrOfSuperUsers; i++)
        _dbContext.Users.Add(new UserDbM
        {
            UserId       = Guid.NewGuid(),
            UserName     = $"superuser{i}",
            Email        = $"superuser{i}@gmail.com",
            PasswordHash = _encryptions.EncryptPasswordToBase64($"superuser{i}"),
            UserRole     = "supusr"
        });

    for (int i = 1; i <= nrOfDbOwners; i++)
        _dbContext.Users.Add(new UserDbM
        {
            UserId       = Guid.NewGuid(),
            UserName     = $"dbo{i}",
            Email        = $"dbo{i}@gmail.com",
            PasswordHash = _encryptions.EncryptPasswordToBase64($"dbo{i}"),
            UserRole     = "dbo"
        });

    await _dbContext.SaveChangesAsync();

    var _info = new UsrInfoDto
    {
        NrUsers      = await _dbContext.Users.CountAsync(i => i.UserRole == "usr"),
        NrSuperUsers = await _dbContext.Users.CountAsync(i => i.UserRole == "supusr"),
        NrDbOwners   = await _dbContext.Users.CountAsync(i => i.UserRole == "dbo")
    };

    return new ResponseItemDto<UsrInfoDto>()
    {
#if DEBUG
        ConnectionString = _dbContext.dbConnection,
#endif
        Item = _info
    };
}
```

---

## Step 16 — Update `Services/AdminServiceDb.cs`

Add `using Models.DTO;` and delegate the new method:

```csharp
public Task<ResponseItemDto<UsrInfoDto>> SeedUsersAsync(int nrOfUsers, int nrOfSuperUsers, int nrOfSysAdmin)
    => _repo.SeedUsersAsync(nrOfUsers, nrOfSuperUsers, nrOfSysAdmin);
```

---

## Step 17 — Update `AppWebApi/Program.cs`

Three changes:

**Add `using Microsoft.OpenApi;` at the top.**

**Call `AddJwtToken` in the startup region:**

```csharp
builder.Configuration.AddSecrets(builder.Environment);
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddJwtToken(builder.Configuration);     // ← add this line
builder.Services.AddDatabaseConnections(builder.Configuration);
```

**Add the Swagger JWT security definition** (inside the `AddSwaggerGen` block, after the `SwaggerDoc` call):

```csharp
c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
    Name = "Authorization",
    Type = SecuritySchemeType.Http,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "JWT Authorization header using the Bearer scheme."
});
c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
{
    {
        new OpenApiSecuritySchemeReference("Bearer", doc, null),
        new List<string>()
    }
});
```

**Register the new login repo and service** (after the existing repo/service block):

```csharp
builder.Services.AddScoped<LoginDbRepos>();
builder.Services.AddScoped<ILoginService, LoginServiceDb>();
```

---

## Step 18 — Update `AppWebApi/Controllers/AdminController.cs`

Add `using Models.DTO;` at the top.

**Add a class-level `[Authorize]` that only applies in release builds** — development and testing work without a token:

```csharp
[ApiController]
[Route("api/[controller]/[action]")]
#if !DEBUG
[Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
    Policy = null, Roles = "dbo")]
#endif
public class AdminController : Controller
```

**Add the `SeedUsers` endpoint** before the constructor:

```csharp
[HttpGet()]
[ActionName("SeedUsers")]
[ProducesResponseType(200, Type = typeof(UsrInfoDto))]
[ProducesResponseType(400, Type = typeof(string))]
public async Task<IActionResult> SeedUsers(
    string countUsr = "32", string countSupUsr = "2", string countDbOwners = "1")
{
    try
    {
        int _countUsr      = int.Parse(countUsr);
        int _countSupUsr   = int.Parse(countSupUsr);
        int _countDbOwners = int.Parse(countDbOwners);

        _logger.LogInformation($"{nameof(SeedUsers)}: usr={_countUsr}, supusr={_countSupUsr}, dbo={_countDbOwners}");

        var _info = await _service.SeedUsersAsync(_countUsr, _countSupUsr, _countDbOwners);
        return Ok(_info);
    }
    catch (Exception ex)
    {
        return BadRequest(ex.Message);
    }
}
```

---

## Step 19 — Add `[Authorize]` to `MusicGroupsController`

Add `using Microsoft.AspNetCore.Authorization;` at the top, then add a class-level attribute requiring at least the `usr` role for all endpoints:

```csharp
[ApiController]
[Route("api/[controller]/[action]")]
[Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
    Policy = null, Roles = "usr, supusr, dbo")]
public class MusicGroupsController : Controller
```

Then add method-level attributes to the four write/privileged endpoints — they require `supusr` or `dbo`:

```csharp
[Authorize(..., Roles = "supusr, dbo")]
[HttpDelete("{id}")]
[ActionName("DeleteItem")]
...

[Authorize(..., Roles = "supusr, dbo")]
[HttpGet()]
[ActionName("ReadItemDto")]    // returns the CU DTO used for edit forms
...

[Authorize(..., Roles = "supusr, dbo")]
[HttpPut("{id}")]
[ActionName("UpdateItem")]
...

[Authorize(..., Roles = "supusr, dbo")]
[HttpPost()]
[ActionName("CreateItem")]
...
```

`Read` and `ReadItem` are covered only by the class-level attribute (any authenticated user).

---

## Step 20 — Add `[Authorize]` to `AlbumsController`

Apply the identical pattern as `MusicGroupsController`. Add `using Microsoft.AspNetCore.Authorization;`, add the class-level `Roles = "usr, supusr, dbo"` attribute, then add method-level `Roles = "supusr, dbo"` to `DeleteItem`, `ReadItemDto`, `UpdateItem`, and `CreateItem`.

---

## Step 21 — Regenerate the EF Core migration

`UserDbM` adds a new `Users` table so a fresh migration is required:

```bash
cd _scripts
./database-rebuild-all.sh sql-music sqlserver docker root ../AppWebApi
```

---

## Step 22 — Update and run `initDatabase.sql`

The script must be rerun after the migration to create the new database objects. Replace the content of `DbContext/SqlScripts/sqlserver/initDatabase.sql` with the version from branch 22 (which adds `gstusr.spLogin`, SQL Server logins, database users, and role grants) then execute it:

```sql
-- Key additions compared to branch 20:

-- Login stored procedure — throws error 999999 on bad credentials
CREATE OR ALTER PROC gstusr.spLogin
    @UserNameOrEmail NVARCHAR(100),
    @UserPassword    NVARCHAR(200),
    @UserId          UNIQUEIDENTIFIER OUTPUT,
    @UserName        NVARCHAR(100)    OUTPUT,
    @UserRole        NVARCHAR(100)    OUTPUT
AS
    SET NOCOUNT ON;
    SET @UserId = NULL; SET @UserName = NULL; SET @UserRole = NULL;

    SELECT TOP 1 @UserId = UserId, @UserName = UserName, @UserRole = UserRole
    FROM dbo.Users
    WHERE ((UserName = @UserNameOrEmail) OR (Email IS NOT NULL AND Email = @UserNameOrEmail))
      AND PasswordHash = @UserPassword;

    IF (@UserId IS NULL)
        ;THROW 999999, 'Login error: wrong user or password', 1
GO

-- SQL Server logins (one per application role)
IF SUSER_ID(N'gstusr') IS NULL CREATE LOGIN gstusr WITH PASSWORD=N'pa$Word1', ...
IF SUSER_ID(N'usr')    IS NULL CREATE LOGIN usr    WITH PASSWORD=N'pa$Word1', ...
-- etc.

-- Database users + schema-based role grants
GRANT SELECT, EXECUTE ON SCHEMA::gstusr TO gstUsrRole;
GRANT SELECT, UPDATE, INSERT ON SCHEMA::supusr TO usrRole;
GRANT SELECT, UPDATE, INSERT, DELETE, EXECUTE ON SCHEMA::supusr TO supUsrRole;
ALTER ROLE db_owner ADD MEMBER dboUser;

-- Role stacking: supusrUser is member of gstUsrRole + usrRole + supUsrRole
```

---

## Verification

Build and run. Swagger shows the "Authorize" padlock button.

Test the login flow:

1. `GET /api/admin/SeedUsers` — creates test users (`user1`…`user32`, `superuser1`, `superuser2`, `dbo1`)
2. Call the login endpoint (to be added in a subsequent step, or test via `LoginDbRepos` directly) with `{ "userNameOrEmail": "superuser1", "userPassword": "superuser1" }` — returns a `LoginUserSessionDto` with `JwtToken.EncryptedToken`
3. Copy the `EncryptedToken`, click "Authorize" in Swagger, paste `<token>` in the Bearer field
4. `GET /api/MusicGroups/Read` — returns `200` (token has `supusr` role)
5. `DELETE /api/MusicGroups/DeleteItem/<guid>` — returns `200` (`supusr` is authorised for deletes)
6. Clear the token, retry `GET /api/MusicGroups/Read` — returns `401`
7. `GET /api/guest/Info` — still returns `200` (no auth required)
