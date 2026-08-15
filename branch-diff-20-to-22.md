# Code differences: `20-database-objects` → `22-jwt-security`

This branch adds the complete JWT authentication and role-based authorisation stack. It introduces a `Users` table, a login stored procedure, a JWT token service, and `[Authorize]` attributes on all write endpoints. It also makes `DbContextExtensions` dynamic: the database connection role is chosen per-request based on the authenticated user's role claim rather than a fixed configuration value.

---

## 1. New NuGet packages — `Configuration/Configuration.csproj`

Three packages are added to the `Configuration` project to support JWT token creation and validation:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.10" />
<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" Version="8.22.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.22.0" />
```

---

## 2. New model and DTOs

### `Models/Interfaces.cs` — `IUser` interface

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

### `Models/User.cs`

Plain domain model, no EF attributes:

```csharp
public class User : IUser
{
    public virtual Guid UserId { get; set; }
    public virtual string UserName { get; set; }
    public virtual string Email { get; set; }
    public virtual string PasswordHash { get; set; }
    public virtual string UserRole { get; set; }
}
```

### `Models/DTO/LoginDto.cs`

```csharp
public class LoginCredentialsDto
{
    public string UserNameOrEmail { get; set; }
    public string UserPassword { get; set; }    // plain text — encrypted before any DB call
}

public class LoginUserSessionDto
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; }
    public string UserRole { get; set; }
    public JwtToken JwtToken { get; set; }      // populated by the service layer after login
}
```

### `Models/DTO/UsrDto.cs`

Response DTO for the user-seeding endpoint:

```csharp
public class UsrInfoDto
{
    public int NrUsers       { get; set; }
    public int NrSuperUsers  { get; set; }
    public int NrDbOwners    { get; set; }
}
```

---

## 3. New `DbModels/UserDbM.cs`

Stored in the `dbo` schema — all database user roles can read from `dbo`. The `UserRole` column holds the string `"gstusr"`, `"usr"`, `"supusr"`, or `"dbo"` which is both the application role and the database connection role:

```csharp
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
```

A `DbSet<UserDbM> Users` is added to `MainDbContext`, and the new `Users` table will appear in the regenerated EF Core migration.

---

## 4. JWT infrastructure — `Configuration/`

### `Configuration/JwtToken.cs`

The return type of `JwtEncryptions.CreateToken`. In debug builds the token also carries the decoded claims so they can be inspected in the response:

```csharp
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

### `Configuration/JwtEncryptions.cs`

Creates and decodes JWT tokens. The signing key and validation parameters come from `JwtOptions` (already defined in the previous branch via user secrets):

```csharp
public class JwtEncryptions
{
    private readonly JwtOptions _jwtOptions;

    public JwtEncryptions(IOptions<JwtOptions> jwtOptions)
        => _jwtOptions = jwtOptions.Value;

    public JwtToken CreateToken(string Role, IDictionary<string, string> userClaims)
    {
        Guid tokenId = Guid.NewGuid();
        var encryptionKey = System.Text.Encoding.ASCII.GetBytes(_jwtOptions.IssuerSigningKey);
        DateTime expireTime = DateTime.UtcNow.AddMinutes(_jwtOptions.LifeTimeMinutes);

        var JWToken = new JwtSecurityToken(
            issuer: _jwtOptions.ValidIssuer,
            audience: _jwtOptions.ValidAudience,
            claims: CreateClaims(tokenId, Role, userClaims),
            notBefore: DateTime.UtcNow,
            expires: expireTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(encryptionKey), SecurityAlgorithms.HmacSha256));

        var token = new JwtToken { EncryptedToken = new JwtSecurityTokenHandler().WriteToken(JWToken) };
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

The private `CreateClaims` method combines the caller's custom claims dictionary with standard ASP.NET Core claim types (`ClaimTypes.Role`, `ClaimTypes.NameIdentifier`, `ClaimTypes.Expiration`).

### `Configuration/Extensions/JWTExtensions.cs`

`AddJwtToken()` registers both the JWT bearer authentication scheme and the `JwtEncryptions` service in one call:

```csharp
public static void AddJwtToken(this IServiceCollection Services, IConfiguration configuration)
{
    Services.AddAuthentication(options => {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options => {
        var jwtOptions = configuration.GetSection(JwtOptions.Position).Get<JwtOptions>();
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
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
```

`options.SaveToken = true` instructs the JWT middleware to store the raw token in the HTTP context so it can be retrieved later with `GetTokenAsync("access_token")` — used by `DbContextExtensions`.

---

## 5. `DbContext/Extensions/DbContextExtensions.cs` — per-request database role

This is the architectural centrepiece of the branch. `AddUserBasedDbContext` is extended to resolve the database connection role from the JWT on each request rather than from a static config value:

```csharp
serviceCollection.AddHttpContextAccessor();     // required to access the current request

serviceCollection.AddDbContext<MainDbContext>((serviceProvider, options) =>
{
    var configuration        = serviceProvider.GetRequiredService<IConfiguration>();
    var databaseConnections  = serviceProvider.GetRequiredService<DatabaseConnections>();
    var jwtEncryptions       = serviceProvider.GetRequiredService<JwtEncryptions>();
    var httpContextAccessor  = serviceProvider.GetRequiredService<IHttpContextAccessor>();

    // default: use the role from appsettings.json (unauthenticated requests)
    var userRole = configuration["DatabaseConnections:DefaultDataUser"];

    var httpContext = httpContextAccessor.HttpContext;
    if (httpContext != null)
    {
        var token = httpContext.GetTokenAsync("access_token").Result;
        if (token != null)
        {
            var claims = jwtEncryptions.GetClaimsFromToken(token);
            userRole = claims["UserRole"];    // override with the role from the JWT
        }
    }

    var conn = databaseConnections.GetDataConnectionDetails(userRole);
    // ... choose provider as before
});
```

The result: every `DbContext` instance is opened using the database credentials of the authenticated user. An unauthenticated request uses `DefaultDataUser` from `appsettings.json` (now set to `gstusr`).

---

## 6. `AppWebApi/appsettings.json` — default user changed to `gstusr`

```json
"DefaultDataUser": "gstusr"
```

Previously `root`. Unauthenticated requests now connect as the lowest-privilege database user, which can only read the `gstusr` schema (the view). Any attempt to access a protected endpoint without a valid JWT will get a `401` and will never open a privileged database connection.

---

## 7. `initDatabase.sql` — expanded with login procedure, logins, users, and roles

The script grows substantially. The additions after the existing view and stored procedure:

**`gstusr.spLogin` stored procedure**

Validates credentials against the `dbo.Users` table using the base64-hashed password. Returns `UserId`, `UserName`, and `UserRole` as output parameters. Throws error 999999 on failure (no silent null return):

```sql
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
```

**SQL Server logins, database users, and role grants**

Four SQL Server logins are created (`gstusr`, `usr`, `supusr`, `dbo`), each with a database user and a role. Role privileges are granted by schema:

| Role | Schema permissions |
|---|---|
| `gstUsrRole` | `SELECT, EXECUTE` on `gstusr` |
| `usrRole` | `SELECT, UPDATE, INSERT` on `supusr` |
| `supUsrRole` | `SELECT, UPDATE, INSERT, DELETE, EXECUTE` on `supusr` |
| `dboUser` | member of `db_owner` (full access) |

Users are stacked into roles (e.g. `supusrUser` is a member of `gstUsrRole`, `usrRole`, and `supUsrRole`) so higher-privilege users inherit all lower-privilege permissions.

---

## 8. `DbRepos/LoginDbRepos.cs` — login stored procedure call

Calls `gstusr.spLogin` via raw ADO.NET. The proc name and parameter types differ per database provider, so the method branches:

```csharp
if (connection is MySqlConnection)
{
    cmd1.CommandText = "gstusr_spLogin";       // MySQL: no schema-qualified names
    // MySqlParameter types
}
else if (connection is NpgsqlConnection)
{
    cmd1.CommandText = "SELECT userid, username, userrole FROM gstusr.\"spLogin\"(@usernameoremail, @userpasswordhash)";
    cmd1.CommandType = CommandType.Text;       // PostgreSQL uses a table-valued function
    // NpgsqlParameter types
}
else
{
    cmd1.CommandText = "gstusr.spLogin";       // SQL Server
    // SqlParameter types
}
```

The password is hashed before it ever leaves the application:

```csharp
userPasswordParam = new SqlParameter("UserPassword", _encryptions.EncryptPasswordToBase64(usrCreds.UserPassword));
```

After `ExecuteScalarAsync()` the output parameters are read back:

```csharp
var info = new LoginUserSessionDto
{
    UserId   = cmd1.Parameters[_usrIdIdx].Value as Guid?,
    UserName = cmd1.Parameters[_usrIdx].Value  as string,
    UserRole = cmd1.Parameters[_roleIdx].Value as string
};
```

If the proc throws (wrong credentials), the exception propagates and the service returns a `400`.

---

## 9. `Services/LoginServiceDb.cs` — creates the JWT after successful login

After the repo confirms the credentials are valid, the service builds the JWT token and attaches it to the session DTO. In debug builds, it immediately decrypts the token and verifies the `UserId` claim matches:

```csharp
public async Task<ResponseItemDto<LoginUserSessionDto>> LoginUserAsync(LoginCredentialsDto usrCreds)
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
    var claims = _jtwEncryptions.GetClaimsFromToken(_usrSession.Item.JwtToken.EncryptedToken);
    if (claims["UserId"] != _usrSession.Item.UserId.ToString())
        throw new InvalidOperationException("JWT token decryption failed - UserId mismatch");
#endif
    return _usrSession;
}
```

---

## 10. `DbRepos/AdminDbRepos.cs` — `SeedUsersAsync`

Creates predictable test users with hashed passwords. All existing users are deleted first. Passwords are simply the username encrypted to base64:

```csharp
_dbContext.Users.Add(new UserDbM
{
    UserId       = Guid.NewGuid(),
    UserName     = $"user{i}",
    Email        = $"user{i}@gmail.com",
    PasswordHash = _encryptions.EncryptPasswordToBase64($"user{i}"),
    UserRole     = "usr"
});
```

After saving, the method queries the counts by role and returns them in `UsrInfoDto`.

---

## 11. `Services/Interfaces.cs` — `ILoginService` and `SeedUsersAsync` added

```csharp
public interface ILoginService
{
    public Task<ResponseItemDto<LoginUserSessionDto>> LoginUserAsync(LoginCredentialsDto usrCreds);
}

// IAdminService gains:
public Task<ResponseItemDto<UsrInfoDto>> SeedUsersAsync(int nrOfUsers, int nrOfSuperUsers, int nrOfSysAdmin);
```

---

## 12. `AppWebApi/Program.cs` — JWT, Swagger security, and new registrations

Three things change:

**`AddJwtToken` called in the startup region:**

```csharp
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddJwtToken(builder.Configuration);     // ← new
builder.Services.AddDatabaseConnections(builder.Configuration);
```

**Swagger gains a JWT security definition** so the "Authorize" button appears in the Swagger UI, letting developers paste a Bearer token:

```csharp
c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement { ... });
```

**New scoped registrations:**

```csharp
builder.Services.AddScoped<LoginDbRepos>();
builder.Services.AddScoped<ILoginService, LoginServiceDb>();
```

Note: `ILoginService` is fully wired up (service + repo registered) but no controller endpoint calls it yet in this branch — the login HTTP endpoint is added in a subsequent step.

---

## 13. `AppWebApi/Controllers/AdminController.cs`

**Class-level `[Authorize]` — release builds only**

The entire `AdminController` is locked to the `dbo` role in release builds. In debug the `#if !DEBUG` condition skips it, so development and testing work without a token:

```csharp
#if !DEBUG
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = null, Roles = "dbo")]
#endif
public class AdminController : Controller
```

**New `SeedUsers` endpoint**

```csharp
[HttpGet()]
[ActionName("SeedUsers")]
[ProducesResponseType(200, Type = typeof(UsrInfoDto))]
public async Task<IActionResult> SeedUsers(
    string countUsr = "32", string countSupUsr = "2", string countDbOwners = "1")
```

Parses the counts and delegates to `IAdminService.SeedUsersAsync`.

---

## 14. `[Authorize]` on `MusicGroupsController` and `AlbumsController`

Both controllers gain the same two-tier authorisation pattern:

**Class level — all endpoints require at least `usr`:**

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = null, Roles = "usr, supusr, dbo")]
public class MusicGroupsController : Controller
```

**Method level — write/delete endpoints additionally require `supusr` or `dbo`:**

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = null, Roles = "supusr, dbo")]
[HttpDelete("{id}")]
[ActionName("DeleteItem")]
...

[Authorize(..., Roles = "supusr, dbo")]
[ActionName("ReadItemDto")]   // needed for editing — returns the CU DTO

[Authorize(..., Roles = "supusr, dbo")]
[ActionName("UpdateItem")]

[Authorize(..., Roles = "supusr, dbo")]
[ActionName("CreateItem")]
```

`Read` and `ReadItem` (read-only list and single item) remain accessible to `usr`. `GuestController` (no `[Authorize]`) remains publicly accessible.

---

## Summary of key concepts introduced

| Concept | Where |
|---|---|
| JWT token creation with custom claims + standard `ClaimTypes` | `JwtEncryptions.CreateToken` |
| `JwtBearerDefaults` authentication scheme registration | `JWTExtensions.AddJwtToken` |
| `options.SaveToken = true` — stores raw token in HTTP context for later retrieval | `JWTExtensions.AddJwtToken` |
| Per-request database role from JWT claim (`GetTokenAsync` → `GetClaimsFromToken`) | `DbContextExtensions.AddUserBasedDbContext` |
| `AddHttpContextAccessor()` — required to access the current HTTP request in DI-resolved code | `DbContextExtensions.AddUserBasedDbContext` |
| Schema-based SQL Server role grants — privilege stacking across roles | `initDatabase.sql` |
| `gstusr.spLogin` — credential validation in the database, throws on failure | `initDatabase.sql`, `LoginDbRepos` |
| Multi-database stored-procedure call (SqlServer / MySQL / PostgreSQL branch) | `LoginDbRepos.LoginUserAsync` |
| Password hashed before leaving the application layer | `LoginDbRepos`, `AdminDbRepos.SeedUsersAsync` |
| `[Authorize(Roles = "...")]` at class and method level — two-tier access control | `MusicGroupsController`, `AlbumsController` |
| `#if !DEBUG [Authorize]` — enforcement only in release builds | `AdminController` |
| Swagger `AddSecurityDefinition` + `AddSecurityRequirement` — Bearer button in Swagger UI | `Program.cs` |
