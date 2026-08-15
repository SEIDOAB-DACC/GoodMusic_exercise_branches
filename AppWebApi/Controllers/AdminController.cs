using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;

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
        public AdminController(ILogger<AdminController> logger,
                    IConfiguration configuration,
                    IOptions<DbConnectionSetsOptions> dbSetOptions,
                    IOptions<AesEncryptionOptions> aesOptions,
                    IOptions<JwtOptions> jwtOptions,
                    IOptions<VersionOptions> versionOptions,
                    IOptions<MySecretOptions> myMessageOptions)
        {
            _logger = logger;

            _dbSetOptions = dbSetOptions.Value;
            _aesOptions = aesOptions.Value;
            _jwtOptions = jwtOptions.Value;
            _versionOptions = versionOptions.Value;
            _configuration = configuration;
            _myMessageOptions = myMessageOptions.Value;
        }
    }
}

