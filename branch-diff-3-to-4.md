# Code Differences: `3-configuration` → `4-dependency-injection`

## Overview

Branch `4-dependency-injection` builds directly on `3-configuration` by introducing the **Dependency Injection (DI) pattern**. Previously, service logic (encryption, database connections) was either absent or manually constructed. In this branch, those concerns are extracted into injectable service classes and registered with the ASP.NET Core DI container.

---

## New Files

### `Configuration/Encryptions.cs`

A new service class that encapsulates AES encryption/decryption logic.

- Receives `IOptions<AesEncryptionOptions>` through its **constructor** — it does not read options itself, it relies on the DI container to supply them.
- Calls `_aesOption.HashKeyIv(Pbkdf2HashToBytes)` in the constructor to derive the key/IV hashes once at construction time.
- Exposes three public methods:
  - `AesEncryptToBase64<T>(T source)` — serializes any object to JSON, then AES-encrypts it and returns Base64.
  - `AesDecryptFromBase64<T>(string encrypted)` — reverses the above.
  - `EncryptPasswordToBase64(string password)` — PBKDF2-hashes a password and returns Base64.

### `Configuration/DatabaseConnections.cs`

A new service class that centralizes database connection resolution.

- Receives `IConfiguration` and `IOptions<DbConnectionSetsOptions>` via constructor injection.
- On construction, it selects the **active dataset** by matching `DatabaseConnections:UseDataSetWithTag` from configuration against the configured data sets. Throws `ArgumentException` if no match is found.
- Exposes:
  - `SetupInfo` property — returns a `SetupInformation` object describing the active environment (secret source, active DB tag, server type, users).
  - `GetDataConnectionDetails(string user)` — looks up and returns a `DbConnectionDetailOptions` for the requested login name, including the resolved connection string.
- Contains a nested `SetupInformation` class that reads the UserSecrets ID from the `Configuration` assembly at runtime and resolves the secret source (User Secrets vs Azure Key Vault).

### `Configuration/MusicGenreService.cs`

Introduces the **interface + multiple implementations** pattern to demonstrate swappable DI registrations.

```csharp
public interface IMusicGenreService
{
    string[] ReadMusicGenres();
}

public class Classics : IMusicGenreService  // returns Classical, Jazz, Rock
public class Modern  : IMusicGenreService  // returns Blues, Pop, Electronic
```

The concrete class to use is decided in `Program.cs`, not in the consumer.

### `AppWebApi/Controllers/MusicGenreController.cs`

A new minimal controller that depends **only on the abstraction** `IMusicGenreService`:

```csharp
public MusicGenreController(IMusicGenreService service) { ... }

[HttpGet("ReadMusicGenres")]
public ActionResult<string[]> ReadMusicGenres() => Ok(_service.ReadMusicGenres());
```

The controller has no knowledge of which implementation (`Classics` or `Modern`) it receives — that decision belongs to the DI registration.

---

## Modified Files

### `AppWebApi/Program.cs`

Three DI registrations are added:

```csharp
// Transient: new instance per request — stateless encryption helper
builder.Services.AddTransient<Encryptions>();

// Singleton: one instance for the app lifetime — reads config once at startup
builder.Services.AddSingleton<DatabaseConnections>();

// Transient: swap the concrete class here to change all consumers at once
// builder.Services.AddTransient<IMusicGenreService, Classics>();
builder.Services.AddTransient<IMusicGenreService, Modern>();
```

The commented-out `Classics` registration illustrates that switching implementations requires **only a single change** in `Program.cs`, with zero changes in controllers or services.

Also adds `using Configuration;` to resolve the new types.

### `AppWebApi/Controllers/AdminController.cs`

**Constructor** is extended to accept the two new services via injection:

```csharp
public AdminController(...,
    Encryptions encryptions,
    DatabaseConnections dbConnections)
{
    ...
    _encryptions = encryptions;
    _dbConnections = dbConnections;
}
```

**Three new endpoints** are added that use these injected services:

| Endpoint | Method | Description |
|---|---|---|
| `GET api/admin` / `Environment` | `IActionResult` | Returns `DatabaseConnections.SetupInformation` — active DB tag, server type, users, secret source. |
| `GET api/admin` / `EncryptedMySecret` | `IActionResult` | AES-encrypts the `MySecretOptions` object and returns it as a Base64 string. |
| `GET api/admin` / `DecryptedMySecret` | `IActionResult` | Accepts an encrypted Base64 string and decrypts it back to a `MySecretOptions` object. |

---

## Key Concepts Demonstrated

| Concept | Where |
|---|---|
| **Constructor injection** of a service into a controller | `AdminController`, `MusicGenreController` |
| **Constructor injection** of `IOptions<T>` into a service | `Encryptions`, `DatabaseConnections` |
| **Transient** lifetime (new instance per request) | `Encryptions`, `IMusicGenreService` |
| **Singleton** lifetime (one instance for app lifetime) | `DatabaseConnections` |
| **Interface abstraction** enabling swappable implementations | `IMusicGenreService` / `Classics` / `Modern` |
| **Single registration point** (`Program.cs`) to switch implementations | `AddTransient<IMusicGenreService, Modern>()` |
