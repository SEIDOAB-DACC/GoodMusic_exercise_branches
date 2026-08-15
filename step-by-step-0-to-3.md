# Step-by-Step Guide: From `0-microsoft-template` to `3-configuration`

Starting point: a fresh `dotnet new webapi` project in `AppWebApi/`.  
Goal: add a `Configuration` class library, the Options Pattern, User Secrets, Swagger, CORS, and an `AdminController`.

---

## Step 1 — Create the `Configuration` class library

```bash
dotnet new classlib -n Configuration -o Configuration --framework net10.0
```

Delete the auto-generated `Class1.cs`:

```bash
rm Configuration/Class1.cs
```

Add the new project to the solution:

```xml
<!-- GoodMusic.slnx -->
<Solution>
  <Project Path="Configuration/Configuration.csproj" />
  <Project Path="AppWebApi/AppWebApi.csproj" />
</Solution>
```

---

## Step 2 — Configure `Configuration/Configuration.csproj`

Replace the scaffolded `.csproj` with the full version below. Key points:

- `UserSecretsId` is declared here (not in `AppWebApi`) so that EF Core migrations can locate secrets at design time by loading this assembly.
- `<AssemblyMetadata>` items use MSBuild property functions to stamp build time, machine, and user into the assembly at compile time.
- Azure Key Vault packages are included now so the project is ready for the production secrets switch in a later branch.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <UserSecretsId>34489944-bd4b-434a-b988-b0f123ee3d2b</UserSecretsId>
  </PropertyGroup>

  <!-- Assembly information -->
  <PropertyGroup>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    <InformationalVersion>1.0.2</InformationalVersion>
    <Copyright>Copyright © Seido AB $([System.DateTime]::UtcNow.Year)</Copyright>
    <Description>This is an API used in Seido's various software developer training courses.</Description>
    <Company>Seido AB</Company>
    <Product>Seido Music simple Api</Product>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
  </PropertyGroup>

  <!-- Build timestamp stamped into assembly metadata at compile time -->
  <ItemGroup>
    <AssemblyMetadata Include="BuildTime" Value="$([System.DateTime]::UtcNow.ToString(&quot;yyyy-MM-dd HH:mm:ss&quot;)) UTC" />
    <AssemblyMetadata Include="BuildMachine" Value="$([System.Environment]::MachineName)" />
    <AssemblyMetadata Include="BuildUser" Value="$([System.Environment]::UserName)" />
    <AssemblyMetadata Include="CompanyUrl" Value="https://seido.se" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Cryptography.KeyDerivation" Version="10.0.10" />
    <PackageReference Include="Microsoft.AspNetCore.Hosting.Abstractions" Version="2.3.11" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.10" />
    <PackageReference Include="Microsoft.Extensions.Configuration.FileExtensions" Version="10.0.10" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.10" />
    <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="10.0.10" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.10" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.10" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.10" />
    <PackageReference Include="Azure.Identity" Version="1.21.0" />
    <PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.11.0" />
    <PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.5.1" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.NewtonsoftJson" Version="10.0.10" />
  </ItemGroup>
</Project>
```

---

## Step 3 — Create the Options classes

Create the folder `Configuration/Options/` and add the following files.

### `Configuration/Options/AesEncryptionOptions.cs`

Bound to the `"AesEncryption"` section of `appsettings.json` or User Secrets. The `HashKeyIv` method accepts a delegate so the caller supplies the actual KDF implementation — keeping crypto logic out of this options class.

```csharp
namespace Configuration.Options;

public class AesEncryptionOptions
{
    public const string Position = "AesEncryption";
    public string Key { get; set; }
    public string Iv { get; set; }
    public string Salt { get; set; }
    public int Iterations { get; set; }

    public byte[] KeyHash { get; private set; }
    public byte[] IvHash { get; private set; }

    public void HashKeyIv(Func<int, string, byte[]> hasher)
    {
        KeyHash = hasher.Invoke(16, Key);
        IvHash = hasher.Invoke(16, Iv);
    }
}
```

### `Configuration/Options/DbConnectionDetailOptions.cs`

A leaf node in the database connection hierarchy: one user login + one connection string.

```csharp
namespace Configuration.Options;

public class DbConnectionDetailOptions
{
    public string DbUserLogin { get; set; }
    public string DbConnection { get; set; }
    public string DbConnectionString { get; init; }
}
```

### `Configuration/Options/DbConnectionSetsOptions.cs`

Bound to `"ConnectionSets"`. Holds two lists of database sets (one for data, one for identity) to support multiple databases and multiple user roles against each.

```csharp
namespace Configuration.Options;

public class DbConnectionSetsOptions
{
    public const string Position = "ConnectionSets";

    public List<DbSetDetailOptions> DataSets { get; set; }
    public List<DbSetDetailOptions> IdentitySets { get; set; }
}

public class DbSetDetailOptions
{
    public string DbTag { get; set; }
    public string DbServer { get; set; }

    public List<DbConnectionDetailOptions> DbConnections { get; set; }
}
```

### `Configuration/Options/JwtOptions.cs`

Bound to `"JwtConfig"`. Covers all JWT bearer token validation parameters.

```csharp
namespace Configuration.Options;

public class JwtOptions
{
    public const string Position = "JwtConfig";

    public int LifeTimeMinutes { get; set; }

    public bool ValidateIssuerSigningKey { get; set; }
    public string IssuerSigningKey { get; set; }

    public bool ValidateIssuer { get; set; } = true;
    public string ValidIssuer { get; set; }

    public bool ValidateAudience { get; set; } = true;
    public string ValidAudience { get; set; }

    public bool RequireExpirationTime { get; set; }
    public bool ValidateLifetime { get; set; } = true;
}
```

### `Configuration/Options/MySecretOptions.cs`

Bound to `"MySecret"` — a minimal demo class used solely to verify that User Secrets injection works end-to-end. Its values are stored in User Secrets, not in `appsettings.json`.

```csharp
namespace Configuration.Options;

public class MySecretOptions
{
    public const string Position = "MySecret";

    public string Message { get; set; }
    public int Number { get; set; }
}
```

### `Configuration/Options/VersionOptions.cs`

Not bound to a config section. Instead, `ReadFromAssembly` is called as a delegate from `Program.cs` and populates the options object using reflection on the `Configuration` assembly. This is how the build-time metadata stamped in Step 2 becomes available at runtime.

```csharp
using System.Reflection;
using System.Text.RegularExpressions;

namespace Configuration.Options;

public class VersionOptions
{
    public string AppEnvironment { get; set; }
    public string AssemblyVersion { get; set; }
    public string FileVersion { get; set; }
    public string InformationalVersion { get; set; }
    public string GitCommitHash { get; set; }

    public string BuildTime { get; set; }
    public string BuildMachine { get; set; }
    public string BuildUser { get; set; }

    public string Company { get; set; }
    public string Product { get; set; }
    public string Description { get; set; }
    public string Copyright { get; set; }
    public string CompanyUrl { get; set; }

    public static VersionOptions ReadFromAssembly(VersionOptions options)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName();

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Unknown";

        options.AppEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        options.AssemblyVersion = assemblyName.Version?.ToString() ?? "Unknown";
        options.FileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
        options.InformationalVersion = informationalVersion;
        options.GitCommitHash = ExtractGitCommitHash(informationalVersion);

        options.BuildTime    = GetAssemblyMetadata(assembly, "BuildTime")    ?? "Unknown";
        options.BuildMachine = GetAssemblyMetadata(assembly, "BuildMachine") ?? "Unknown";
        options.BuildUser    = GetAssemblyMetadata(assembly, "BuildUser")    ?? "Unknown";

        options.Company     = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company           ?? "Unknown";
        options.Product     = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product           ?? "Unknown";
        options.Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description   ?? "Unknown";
        options.Copyright   = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright       ?? $"Copyright © Unknown {DateTime.UtcNow.Year}";
        options.CompanyUrl  = GetAssemblyMetadata(assembly, "CompanyUrl") ?? "Unknown";

        return options;
    }

    private static string GetAssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attr => attr.Key == key)?.Value;

    private static string ExtractGitCommitHash(string informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion)) return "Unknown";

        // git appends the commit hash after '+' in the InformationalVersion
        var match = Regex.Match(informationalVersion, @"\+([a-fA-F0-9]+)");
        if (match.Success)
        {
            var hash = match.Groups[1].Value;
            return hash.Length > 10 ? hash[..10] : hash;
        }
        return "Unknown";
    }
}
```

---

## Step 4 — Update `AppWebApi/AppWebApi.csproj`

Three changes:
1. Turn off nullable (`disable`) to match the `Configuration` project.
2. Replace `Microsoft.OpenApi` with `Swashbuckle.AspNetCore` and add `NewtonsoftJson`.
3. Add a `<ProjectReference>` to `Configuration`.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(RunConfiguration)' == 'https' " />
  <PropertyGroup Condition=" '$(RunConfiguration)' == 'http' " />

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.10" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="10.2.3" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.NewtonsoftJson" Version="10.0.10" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
  </ItemGroup>

</Project>
```

---

## Step 5 — Update `AppWebApi/appsettings.json`

Replace the minimal two-key file. The logging section is expanded with per-provider log levels. Two new top-level sections are added:

- `ApplicationSecrets` — declares which secret backend is active (`UserSecrets` here, `AzureKeyVault` in production).
- `DatabaseConnections` — the active dataset tag and user roles consumed by `DbConnectionSetsOptions` and the Swagger description.

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information"
        },
        "Console": {
            "LogLevel": {
                "Services": "Information",
                "AppWebApi.Controllers": "None",
                "DbRepos": "None"
            }
        },
        "InMemory": {
            "LogLevel": {
                "Services": "Information",
                "AppWebApi.Controllers": "Information",
                "DbRepos": "Information"
            }
        }
    },

    "AllowedHosts": "*",

    "ApplicationSecrets": {
        "SecretStorage": "UserSecrets"
    },

    "DatabaseConnections": {
        "DefaultDataUser": "root",
        "MigrationUser": "root",
        "UseDataSetWithTag": "sql-music.sqlserver.docker"
    }
}
```

---

## Step 6 — Add User Secrets for `MySecretOptions`

The `MySecretOptions` values must not be committed to source control. Store them as User Secrets against the `Configuration` assembly (which owns the `UserSecretsId`):

```bash
dotnet user-secrets --project Configuration set "MySecret:Message" "Hello from user secrets"
dotnet user-secrets --project Configuration set "MySecret:Number" "42"
```

---

## Step 7 — Replace `AppWebApi/Program.cs`

The new `Program.cs` has five responsibilities:

1. **CORS** — permissive global policy for JS/React frontends.
2. **JSON serialization** — Newtonsoft.Json with reference-loop handling (needed once EF entities arrive).
3. **Configuration bootstrap** — load `appsettings.json` and User Secrets via the `Configuration` assembly so the same code path works for EF migrations.
4. **Options registration** — bind each options class to its config section.
5. **Swagger** — replaces `MapOpenApi()`, enabled unconditionally for training.

```csharp
using Configuration.Options;

var builder = WebApplication.CreateBuilder(args);

// NOTE: global cors policy needed for JS and React frontends
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

builder.Services.AddControllers().AddNewtonsoftJson(options =>
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
builder.Services.AddEndpointsApiExplorer();

#region Configuration bootstrap
// User Secrets are loaded via the Configuration assembly, not AppWebApi.
// This is required so that EF Core migrations (which load the assembly at
// design time) can find the same secrets as the running application.
var currentDir = Directory.GetCurrentDirectory();
var assembly = System.Reflection.Assembly.Load("Configuration");
builder.Configuration
    .SetBasePath(Path.Combine(currentDir, "../AppWebApi"))
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets(assembly);

builder.Services.Configure<AesEncryptionOptions>(
    options => builder.Configuration.GetSection(AesEncryptionOptions.Position).Bind(options));

builder.Services.Configure<JwtOptions>(
    options => builder.Configuration.GetSection(JwtOptions.Position).Bind(options));

builder.Services.Configure<DbConnectionSetsOptions>(
    options => builder.Configuration.GetSection(DbConnectionSetsOptions.Position).Bind(options));

// VersionOptions is populated from the assembly, not from a config section
builder.Services.Configure<VersionOptions>(options => VersionOptions.ReadFromAssembly(options));
#endregion

builder.Services.Configure<MySecretOptions>(
    options => builder.Configuration.GetSection(MySecretOptions.Position).Bind(options));

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Seido Music simple API",
#if DEBUG
        Version = "v2.0 DEBUG",
#else
        Version = "v2.0",
#endif
        Description = "This is an API used in Seido's various software developer training courses."
            + $"<br>DataSet: {builder.Configuration["DatabaseConnections:UseDataSetWithTag"]}"
            + $"<br>DefaultDataUser: {builder.Configuration["DatabaseConnections:DefaultDataUser"]}"
    });
});

var app = builder.Build();

// Swagger enabled unconditionally for this training project
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Seido Music simple API v2.0");
});

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## Step 8 — Delete the template files

```bash
rm AppWebApi/WeatherForecast.cs
rm AppWebApi/Controllers/WeatherForecastController.cs
rm AppWebApi/appsettings.Development.json
rm AppWebApi/AppWebApi.http
```

---

## Step 9 — Create `AppWebApi/Controllers/AdminController.cs`

The controller demonstrates the Options Pattern in action. Every `IOptions<T>` is injected via the constructor, and `.Value` is called once to unwrap the concrete options object.

```csharp
using Microsoft.AspNetCore.Mvc;
using Configuration.Options;
using Microsoft.Extensions.Options;

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AdminController : Controller
    {
        readonly ILogger<AdminController> _logger;
        readonly DbConnectionSetsOptions _dbSetOptions;
        readonly AesEncryptionOptions _aesOptions;
        readonly JwtOptions _jwtOptions;
        readonly VersionOptions _versionOptions;
        readonly IConfiguration _configuration;
        readonly MySecretOptions _myMessageOptions;

        // GET api/admin/Version
        [HttpGet]
        [ActionName("Version")]
        [ProducesResponseType(typeof(VersionOptions), 200)]
        public IActionResult Version()
        {
            try { return Ok(_versionOptions); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving version information");
                return BadRequest(ex.Message);
            }
        }

        // GET api/admin/MySecret
        [HttpGet]
        [ActionName("MySecret")]
        [ProducesResponseType(typeof(MySecretOptions), 200)]
        public IActionResult MySecret()
        {
            try { return Ok(_myMessageOptions); }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(MySecret)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        public AdminController(
            ILogger<AdminController> logger,
            IConfiguration configuration,
            IOptions<DbConnectionSetsOptions> dbSetOptions,
            IOptions<AesEncryptionOptions> aesOptions,
            IOptions<JwtOptions> jwtOptions,
            IOptions<VersionOptions> versionOptions,
            IOptions<MySecretOptions> myMessageOptions)
        {
            _logger = logger;
            _configuration = configuration;
            _dbSetOptions = dbSetOptions.Value;
            _aesOptions = aesOptions.Value;
            _jwtOptions = jwtOptions.Value;
            _versionOptions = versionOptions.Value;
            _myMessageOptions = myMessageOptions.Value;
        }
    }
}
```

---

## Step 10 — Update `.vscode/launch.json`

Change the browser URL that opens on launch from the old weather endpoint to the Swagger UI:

```json
"uriFormat": "%s/swagger"
```

---

## Step 11 — Build and verify

```bash
dotnet build GoodMusic.slnx
dotnet run --project AppWebApi
```

Open `https://localhost:{port}/swagger`. You should see two endpoints under **Admin**:

- `GET /api/admin/Version` — returns assembly and build metadata.
- `GET /api/admin/MySecret` — returns the values stored in User Secrets.
