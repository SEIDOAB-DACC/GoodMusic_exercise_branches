using Microsoft.AspNetCore.Mvc;

using Models.Interfaces;
using Models.DTO;
using Services;
using System.Text.RegularExpressions;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace AppWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class MusicGroupsController : Controller
    {
        readonly IMusicGroupsService _service = null;
        readonly ILogger<MusicGroupsController> _logger = null;

        [HttpGet()]
        [ActionName("Read")]
        [ProducesResponseType(200, Type = typeof(ResponsePageDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> Read(string seeded = "true", string flat = "true",
            string filter = null, string pageNr = "0", string pageSize = "10")
        {
            try
            {
                bool seededArg = bool.Parse(seeded);
                bool flatArg = bool.Parse(flat);
                int pageNrArg = int.Parse(pageNr);
                int pageSizeArg = int.Parse(pageSize);

                // RegEx check to ensure filter only contains a-z, 0-9, and spaces
                if (!string.IsNullOrEmpty(filter) && !Regex.IsMatch(filter, @"^[a-zA-Z0-9\s]*$"))
                {
                    throw new ArgumentException("Filter can only contain letters (a-z), numbers (0-9), and spaces.");
                }
                
                _logger.LogInformation($"{nameof(Read)}: {nameof(seededArg)}: {seededArg}, {nameof(flatArg)}: {flatArg}, " +
                    $"{nameof(pageNrArg)}: {pageNrArg}, {nameof(pageSizeArg)}: {pageSizeArg}");

                var resp = await _service.ReadMusicGroupsAsync(seededArg, flatArg, filter?.Trim().ToLower(), pageNrArg, pageSizeArg);     
                return Ok(resp);     
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(Read)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet()]
        [ActionName("ReadItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        public async Task<IActionResult> ReadItem(string id = null, string flat = "false")
        {
            try
            {
                var idArg = Guid.Parse(id);
                bool flatArg = bool.Parse(flat);

                _logger.LogInformation($"{nameof(ReadItem)}: {nameof(idArg)}: {idArg}, {nameof(flatArg)}: {flatArg}");
                
                var item = await _service.ReadMusicGroupAsync(idArg, flatArg);
                if (item == null) throw new ArgumentException ($"Item with id {id} does not exist");

                return Ok(item);         
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ReadItem)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [ActionName("DeleteItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> DeleteItem(string id)
        {
            try
            {
                var idArg = Guid.Parse(id);

                _logger.LogInformation($"{nameof(DeleteItem)}: {nameof(idArg)}: {idArg}");
                
                var item = await _service.DeleteMusicGroupAsync(idArg);
                if (item == null) throw new ArgumentException ($"Item with id {id} does not exist");
        
                _logger.LogInformation($"item {idArg} deleted");
                return Ok(item);                
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(DeleteItem)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet()]
        [ActionName("ReadItemDto")]
        [ProducesResponseType(200, Type = typeof(MusicGroupCUdto))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        public async Task<IActionResult> ReadItemDto(string id = null)
        {
            try
            {
                var idArg = Guid.Parse(id);

                _logger.LogInformation($"{nameof(ReadItemDto)}: {nameof(idArg)}: {idArg}");

                var item = await _service.ReadMusicGroupAsync(idArg, false);
                if (item == null) throw new ArgumentException($"Item with id {id} does not exist");

                return Ok(
                    new ResponseItemDto<MusicGroupCUdto>() {
#if DEBUG
                    ConnectionString = item.ConnectionString,
#endif
                    Item = new MusicGroupCUdto(item.Item)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ReadItemDto)}: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        [ActionName("UpdateItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> UpdateItem(string id, [FromBody] MusicGroupCUdto item)
        {
            try
            {
                var idArg = Guid.Parse(id);
                _logger.LogInformation($"{nameof(UpdateItem)}: {nameof(idArg)}: {idArg}");
                if (item.MusicGroupId != idArg) throw new ArgumentException("Id mismatch");

                var _item = await _service.UpdateMusicGroupAsync(item);
                _logger.LogInformation($"item {idArg} updated");
               
                return Ok(_item);             
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(UpdateItem)}: {ex.Message}");
                return BadRequest($"Could not update. Error {ex.Message}");
            }
        }

        [HttpPost()]
        [ActionName("CreateItem")]
        [ProducesResponseType(200, Type = typeof(ResponseItemDto<IMusicGroup>))]
        [ProducesResponseType(400, Type = typeof(string))]
        public async Task<IActionResult> CreateItem([FromBody] MusicGroupCUdto item)
        {
            try
            {
               _logger.LogInformation($"{nameof(CreateItem)}:");
                
                var _item = await _service.CreateMusicGroupAsync(item);
                _logger.LogInformation($"item {_item.Item.MusicGroupId} created");

                return Ok(_item);       
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(CreateItem)}: {ex.Message}");
                return BadRequest($"Could not create. Error {ex.Message}");
            }
        }

        public MusicGroupsController(IMusicGroupsService service, ILogger<MusicGroupsController> logger)
        {
            _service = service;
            _logger = logger;
        }
    }
}

