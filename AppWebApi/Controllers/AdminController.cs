using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;

using Services;
using Configuration;
using Configuration.Options;

using Microsoft.Extensions.Options;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]   
    public class AdminController : Controller
    {
        readonly ILogger<AdminController> _logger;
        private readonly DbConnectionSetsOptions _dbSetOptions;
        readonly AesEncryptionOptions _aesOptions;
        readonly JwtOptions _jwtOptions;
        readonly VersionOptions _versionOptions;
        readonly IConfiguration _configuration;
        readonly MySecretOptions _myMessageOptions;
        readonly Encryptions _encryptions = null;
        readonly DatabaseConnections _dbConnections = null;
        readonly IAdminService _service;


        //GET: api/admin/connectionstring
        [HttpGet()]
        [ActionName("ConnectionString")]
        [ProducesResponseType(200, Type = typeof(string))]
        public IActionResult ConnectionString()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("SqlServerDocker");

                _logger.LogInformation($"{nameof(ConnectionString)}:\n{JsonConvert.SerializeObject(connectionString)}");
                return Ok(connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ConnectionString)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }
        
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
        
        //GET: api/admin/version
        [HttpGet()]
        [ActionName("Version")]
        [ProducesResponseType(typeof(VersionOptions), 200)]
        public IActionResult Version()
        {
            try
            {
                return Ok(_versionOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving version information");
                return BadRequest(ex.Message);
            }
        }

        //GET: api/admin/MySecret
        [HttpGet()]
        [ActionName("MySecret")]
        [ProducesResponseType(200, Type = typeof(MySecretOptions))]
        public IActionResult MySecret()
        {
            try
            {
                return Ok(_myMessageOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(MySecret)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

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

        //GET: api/admin/seed?count={count}
        [HttpGet()]
        [ActionName("Seed")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> Seed(int seedCount)
        {
            try
            {
                _logger.LogInformation($"{nameof(Seed)}");
                await _service.SeedAsync(seedCount);

                return Ok($"Seeding {seedCount} items completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(Seed)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        //GET: api/admin/log
        [HttpGet()]
        [ActionName("Log")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<LogMessage>))]
        public async Task<IActionResult> Log([FromServices] ILoggerProvider _loggerProvider)
        {
            //Note the way to get the LoggerProvider, not the logger from Services via DI
            if (_loggerProvider is InMemoryLoggerProvider cl)
            {
                return Ok(await cl.MessagesAsync);
            }
            return Ok("No messages in log");
        }

        public AdminController(ILogger<AdminController> logger,
                    IConfiguration configuration,
                    IOptions<DbConnectionSetsOptions> dbSetOptions,
                    IOptions<AesEncryptionOptions> aesOptions,
                    IOptions<JwtOptions> jwtOptions,
                    IOptions<VersionOptions> versionOptions,
                    IOptions<MySecretOptions> myMessageOptions,
                    Encryptions encryptions, DatabaseConnections dbConnections,
                    IAdminService service)
        {
            _logger = logger;

            _dbSetOptions = dbSetOptions.Value;
            _aesOptions = aesOptions.Value;
            _jwtOptions = jwtOptions.Value;
            _versionOptions = versionOptions.Value;
            _configuration = configuration;
            _myMessageOptions = myMessageOptions.Value;

            _encryptions = encryptions;
            _dbConnections = dbConnections;

            _service = service;

        }
    }
}
