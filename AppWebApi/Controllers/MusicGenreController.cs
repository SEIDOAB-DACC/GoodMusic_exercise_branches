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

        // Service endpoints
        [HttpGet("ReadMusicGenres")]
        public ActionResult<string[]> ReadMusicGenres()
        {

            return Ok(_service.ReadMusicGenres());
        }
    }
}
