# Step-by-step: from `3-configuration` to `4-dependency-injection`

The goal of this branch is to move service logic out of controllers and into **injectable classes**, then register those classes with the ASP.NET Core DI container. Controllers receive what they need through their **constructor** — they never `new` up services themselves.

---

## Step 1 — Create the `IMusicGenreService` interface and implementations

This is the simplest example of the pattern: one interface, two concrete classes. The controller will only know about the interface.

Create **`Configuration/MusicGenreService.cs`**:

```csharp
namespace Configuration;

public interface IMusicGenreService
{
    public string[] ReadMusicGenres();
}

public class Classics : IMusicGenreService
{
    public string[] ReadMusicGenres() => new string[] { "Classical", "Jazz", "Rock" };
}

public class Modern : IMusicGenreService
{
    public string[] ReadMusicGenres() => new string[] { "Blues", "Pop", "Electronic" };
}
```

No dependencies, no constructor needed. These are plain classes that satisfy the interface.

---

## Step 2 — Create the `Encryptions` service

The AES encryption logic becomes a dedicated injectable class. It receives its configuration options through constructor injection rather than reading them itself.

Create **`Configuration/Encryptions.cs`**:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

using Configuration.Options;

namespace Configuration;

public class Encryptions
{
    private readonly AesEncryptionOptions _aesOption;

    public Encryptions(IOptions<AesEncryptionOptions> aesOptions)
    {
        _aesOption = aesOptions.Value;
        _aesOption.HashKeyIv(Pbkdf2HashToBytes);
    }

    public string AesEncryptToBase64<T>(T sourceToEncrypt)
    {
        string stringToEncrypt = JsonConvert.SerializeObject(sourceToEncrypt);
        byte[] dataset = System.Text.Encoding.Unicode.GetBytes(stringToEncrypt);

        byte[] encryptedBytes;
        using (SymmetricAlgorithm algorithm = Aes.Create())
        using (ICryptoTransform encryptor = algorithm.CreateEncryptor(_aesOption.KeyHash, _aesOption.IvHash))
        {
            encryptedBytes = encryptor.TransformFinalBlock(dataset, 0, dataset.Length);
        }

        return Convert.ToBase64String(encryptedBytes);
    }

    public T AesDecryptFromBase64<T>(string encryptedBase64)
    {
        byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);

        byte[] decryptedBytes;
        using (SymmetricAlgorithm algorithm = Aes.Create())
        using (ICryptoTransform decryptor = algorithm.CreateDecryptor(_aesOption.KeyHash, _aesOption.IvHash))
        {
            decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        }

        string decryptedString = System.Text.Encoding.Unicode.GetString(decryptedBytes);
        return JsonConvert.DeserializeObject<T>(decryptedString);
    }

    private byte[] Pbkdf2HashToBytes(int nrBytes, string password)
    {
        return KeyDerivation.Pbkdf2(
            password: password,
            salt: Encoding.UTF8.GetBytes(_aesOption.Salt),
            prf: KeyDerivationPrf.HMACSHA512,
            iterationCount: _aesOption.Iterations,
            numBytesRequested: nrBytes);
    }

    public string EncryptPasswordToBase64(string password)
    {
        byte[] encrypted = Pbkdf2HashToBytes(64, password);
        return Convert.ToBase64String(encrypted);
    }
}
```

Notice: `Encryptions` does **not** call `new AesEncryptionOptions()`. The DI container calls the constructor and passes `IOptions<AesEncryptionOptions>` automatically because it was registered in `Program.cs` in branch 3.

---

## Step 3 — Create the `DatabaseConnections` service

This service centralises connection-string resolution and environment diagnostics. It selects the active dataset at construction time and exposes a `SetupInfo` property and a `GetDataConnectionDetails` method.

Create **`Configuration/DatabaseConnections.cs`**:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration.UserSecrets;

using Configuration.Options;

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
        MigrationUser   = _configuration["DatabaseConnections:MigrationUser"],

        DataConnectionTag = _activeDataSet.DbTag,
        DataConnectionServer = _activeDataSet.DbServer.Trim().ToLower() switch
        {
            "sqlserver"  => DatabaseServer.SQLServer,
            "mysql"      => DatabaseServer.MySql,
            "postgresql" => DatabaseServer.PostgreSql,
            "sqlite"     => DatabaseServer.SQLite,
            _ => throw new NotSupportedException($"DbServer {_activeDataSet.DbServer} not supported")
        },
    };

    public DbConnectionDetailOptions GetDataConnectionDetails(string user)
        => GetLoginDetails(user, _activeDataSet);

    DbConnectionDetailOptions GetLoginDetails(string user, DbSetDetailOptions dataSet)
    {
        if (string.IsNullOrEmpty(user) || string.IsNullOrWhiteSpace(user))
            throw new ArgumentNullException(nameof(user));

        var conn = dataSet.DbConnections.First(
            m => m.DbUserLogin.Trim().ToLower() == user.Trim().ToLower());

        return new DbConnectionDetailOptions
        {
            DbUserLogin          = conn.DbUserLogin,
            DbConnection         = conn.DbConnection,
            DbConnectionString   = _configuration.GetConnectionString(conn.DbConnection)
        };
    }

    public DatabaseConnections(IConfiguration configuration,
                               IOptions<DbConnectionSetsOptions> dbSetOption)
    {
        _configuration = configuration;
        _options       = dbSetOption.Value;

        _activeDataSet = _options.DataSets.FirstOrDefault(
            ds => ds.DbTag.Trim().ToLower() ==
                  configuration["DatabaseConnections:UseDataSetWithTag"].Trim().ToLower());

        if (_activeDataSet == null)
            throw new ArgumentException(
                $"Dataset with DbTag {configuration["DatabaseConnections:UseDataSetWithTag"]} not found");
    }

    // -------------------------------------------------------------------------

    public class SetupInformation
    {
        private string _userSecretsId = null;

        public string SecretSource       { get; init; }
        public string DataConnectionTag  { get; init; }
        public string DefaultDataUser    { get; init; }
        public string MigrationUser      { get; init; }
        public DatabaseServer DataConnectionServer { get; init; }

        // explicit string form for clean JSON serialization
        public string DataConnectionServerString => DataConnectionServer.ToString();

        public string AppEnvironment => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        public string SecretId => SecretSource switch
        {
            "AzureKeyVault" => $"{Environment.GetEnvironmentVariable("AzureKeyVault_kvAccessParams_kvSecret")}",
            _               => _userSecretsId
        };

        public SetupInformation()
        {
            var assembly = System.Reflection.Assembly.Load("Configuration");
            var attr = assembly.GetCustomAttributes(typeof(UserSecretsIdAttribute), false)
                               .FirstOrDefault() as UserSecretsIdAttribute;
            _userSecretsId = attr?.UserSecretsId;
        }
    }
}
```

`DatabaseConnections` is constructed **once** for the lifetime of the application (singleton). It reads and validates configuration at startup so problems surface immediately, not on the first request.

---

## Step 4 — Register the services in `Program.cs`

Open **`AppWebApi/Program.cs`** and make three additions.

**4a.** Add the `using` directive at the top so the new types are visible:

```csharp
// before (line 1)
using Configuration.Options;

// after
using Configuration;
using Configuration.Options;
```

**4b.** Register `Encryptions` as **transient** (a fresh instance per request is fine — it holds no mutable state after construction):

```csharp
builder.Services.Configure<AesEncryptionOptions>(
    options => builder.Configuration.GetSection(AesEncryptionOptions.Position).Bind(options));

// ADD THIS LINE:
builder.Services.AddTransient<Encryptions>();
```

**4c.** Register `DatabaseConnections` as **singleton** (it reads configuration once and the result never changes):

```csharp
builder.Services.Configure<DbConnectionSetsOptions>(
    options => builder.Configuration.GetSection(DbConnectionSetsOptions.Position).Bind(options));

// ADD THIS LINE:
builder.Services.AddSingleton<DatabaseConnections>();
```

**4d.** Register `IMusicGenreService` as **transient**, mapping the interface to a concrete class. The commented-out line shows how swapping implementations is a one-line change:

```csharp
builder.Services.Configure<MySecretOptions>(
    options => builder.Configuration.GetSection(MySecretOptions.Position).Bind(options));

// ADD THESE LINES:
//builder.Services.AddTransient<IMusicGenreService, Classics>();
builder.Services.AddTransient<IMusicGenreService, Modern>();
```

---

## Step 5 — Create `MusicGenreController`

Create **`AppWebApi/Controllers/MusicGenreController.cs`**:

```csharp
using Microsoft.AspNetCore.Mvc;
using Configuration;

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MusicGenreController : ControllerBase
    {
        private readonly IMusicGenreService _service;

        public MusicGenreController(IMusicGenreService service)
        {
            _service = service;
        }

        [HttpGet("ReadMusicGenres")]
        public ActionResult<string[]> ReadMusicGenres()
        {
            return Ok(_service.ReadMusicGenres());
        }
    }
}
```

The controller declares a dependency on `IMusicGenreService`. The DI container resolves the registration from Step 4d and injects a `Modern` instance automatically.

---

## Step 6 — Update `AdminController`

Open **`AppWebApi/Controllers/AdminController.cs`** and make the following changes.

**6a.** Add two new fields below the existing ones:

```csharp
readonly MySecretOptions _myMessageOptions;
// ADD:
readonly Encryptions _encryptions = null;
readonly DatabaseConnections _dbConnections = null;
```

**6b.** Add the `Environment` endpoint before `Version`:

```csharp
//GET: api/admin/environment
[HttpGet()]
[ActionName("Environment")]
[ProducesResponseType(200, Type = typeof(DatabaseConnections.SetupInformation))]
public IActionResult Environment()
{
    try
    {
        var info = _dbConnections.SetupInfo;
        _logger.LogInformation($"{nameof(Environment)}:\n{JsonConvert.SerializeObject(info)}");
        return Ok(info);
    }
    catch (Exception ex)
    {
        _logger.LogError($"{nameof(Environment)}: {ex.Message}");
        return BadRequest(ex.Message);
    }
}
```

**6c.** Add the two encryption endpoints after `MySecret`:

```csharp
//GET: api/admin/EncryptedMySecret
[HttpGet()]
[ActionName("EncryptedMySecret")]
[ProducesResponseType(200, Type = typeof(string))]
[ProducesResponseType(400, Type = typeof(string))]
public IActionResult EncryptedMySecret()
{
    try
    {
        _logger.LogInformation($"{nameof(EncryptedMySecret)}");
        var encrypted = _encryptions.AesEncryptToBase64<MySecretOptions>(_myMessageOptions);
        return Ok(encrypted);
    }
    catch (Exception ex)
    {
        _logger.LogError($"{nameof(EncryptedMySecret)}: {ex.Message}");
        return BadRequest(ex.Message);
    }
}

//GET: api/admin/decryptedMySecret
[HttpGet()]
[ActionName("DecryptedMySecret")]
[ProducesResponseType(200, Type = typeof(MySecretOptions))]
[ProducesResponseType(400, Type = typeof(string))]
public IActionResult DecryptedMySecret(string encryptedMySecret)
{
    try
    {
        _logger.LogInformation($"{nameof(DecryptedMySecret)}");
        var decrypted = _encryptions.AesDecryptFromBase64<MySecretOptions>(encryptedMySecret);
        return Ok(decrypted);
    }
    catch (Exception ex)
    {
        _logger.LogError($"{nameof(DecryptedMySecret)}: {ex.Message}");
        return BadRequest(ex.Message);
    }
}
```

**6d.** Extend the constructor signature and assign the new fields:

```csharp
// before:
public AdminController(ILogger<AdminController> logger,
            IConfiguration configuration,
            IOptions<DbConnectionSetsOptions> dbSetOptions,
            IOptions<AesEncryptionOptions> aesOptions,
            IOptions<JwtOptions> jwtOptions,
            IOptions<VersionOptions> versionOptions,
            IOptions<MySecretOptions> myMessageOptions)

// after:
public AdminController(ILogger<AdminController> logger,
            IConfiguration configuration,
            IOptions<DbConnectionSetsOptions> dbSetOptions,
            IOptions<AesEncryptionOptions> aesOptions,
            IOptions<JwtOptions> jwtOptions,
            IOptions<VersionOptions> versionOptions,
            IOptions<MySecretOptions> myMessageOptions,
            Encryptions encryptions, DatabaseConnections dbConnections)
```

And in the constructor body, append:

```csharp
_myMessageOptions = myMessageOptions.Value;

// ADD:
_encryptions  = encryptions;
_dbConnections = dbConnections;
```

---

## Summary

| Step | File | What changed |
|------|------|--------------|
| 1 | `Configuration/MusicGenreService.cs` | New — interface + two implementations |
| 2 | `Configuration/Encryptions.cs` | New — AES encrypt/decrypt service |
| 3 | `Configuration/DatabaseConnections.cs` | New — DB connection resolver |
| 4 | `AppWebApi/Program.cs` | Register 3 services + add `using Configuration` |
| 5 | `AppWebApi/Controllers/MusicGenreController.cs` | New — controller consuming `IMusicGenreService` |
| 6 | `AppWebApi/Controllers/AdminController.cs` | 2 new fields, 3 new endpoints, extended constructor |

### Why these lifetimes?

| Service | Lifetime | Reason |
|---------|----------|--------|
| `Encryptions` | `Transient` | Stateless after construction; cheap to create |
| `DatabaseConnections` | `Singleton` | Reads and validates config once; result is immutable |
| `IMusicGenreService` | `Transient` | Stateless; one instance per request is safe |
