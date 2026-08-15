using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

using Models.DTO;
using Services;
using System.Text.RegularExpressions;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class GuestController : Controller
    {
        readonly IAdminService _service;
        readonly ILoginService _loginService;
        readonly ILogger<GuestController> _logger = null;

        //GET: api/guest/info
        [HttpGet()]
        [ActionName("Info")]
        [ProducesResponseType(200, Type = typeof(GstUsrInfoAllDto))]
        public async Task<IActionResult> Info()
        {
            try
            {
                var info = await _service.GuestInfoAsync();

                _logger.LogInformation($"{nameof(Info)}:\n{JsonConvert.SerializeObject(info)}");
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(Info)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        //POST: api/guest/LoginUser
        [HttpPost]
        [ActionName("LoginUser")]
        [ProducesResponseType(200, Type = typeof(LoginUserSessionDto))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> LoginUser([FromBody] LoginCredentialsDto userCreds)
        {
            _logger.LogInformation("LoginUser initiated");

            try
            {
                // Note: Validate userCreds to avoid sql injection
                // UserName and password - Allow only Only a-z or A-Z or 0-9 between 4-12 characters
                var pSimple = @"^([a-z]|[A-Z]|[0-9]){4,12}$";

                //RFC2822 email pattern from regexr.com
                var pEmail = @"[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?";

                //UserNameOrEmail
                var pUNoE = @$"({pSimple})|({pEmail})";

                // Match the regular expression pattern against a text string.
                Regex r = new Regex(pUNoE, RegexOptions.IgnoreCase);
                if (!r.Match(userCreds.UserNameOrEmail).Success) throw new ArgumentException("Wrong username format");

                // Match the regular expression pattern against a text string.
                r = new Regex(pSimple, RegexOptions.IgnoreCase);
                if (!r.Match(userCreds.UserPassword).Success) throw new ArgumentException("Wrong password format");

                //With validated credentials proceed to login
                var _usr = await _loginService.LoginUserAsync(userCreds);
                 _logger.LogInformation($"{_usr.Item.UserName} logged in");
                    return Ok(_usr);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Login Error: {ex.Message}");
                return BadRequest($"Login Error: {ex.Message}");
            }
        }

        public GuestController(IAdminService service, ILoginService loginService,
                ILogger<GuestController> logger)
        {
            _service = service;
            _loginService = loginService;
            _logger = logger;
        }
    }
}