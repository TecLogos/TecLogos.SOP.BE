using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebModel.SOP;
using TecLogos.SOP.WebApi.Controllers.Base;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
    [Authorize]
    [Route("api/v1/[controller]")]
    [ApiController]
    public class EGDetailController : CustomControllerBase
    {
        private readonly IEGDetailBAL _bal;
        private readonly ILogger<EGDetailController> _logger;

        public EGDetailController(
            IEGDetailBAL bal,
            ILogger<EGDetailController> logger)
        {
            _bal = bal;
            _logger = logger;
        }

        [HttpGet("list")]
        public async Task<ActionResult<List<EGDetail>>> GetAll()
            => Ok(await _bal.GetAll());

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
            => Ok(await _bal.Get(id));

        [HttpPost]
        public async Task<IActionResult> Create(EGDetail model)
        {
            var id = await _bal.Create(model, GetUser());
            return Ok(id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(EGDetail model)
            => Ok(await _bal.Update(model, GetUser()));

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
            => Ok(await _bal.Delete(id, GetUser()));

        private Guid GetUser()
            => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}