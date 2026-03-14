using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.DataModel.SOP;
using TecLogos.SOP.WebApi.Controllers.Base;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
    [Authorize]
    [Route("api/v1/[controller]")]
    [ApiController]
    public class RolesController : CustomControllerBase
    {
            private readonly IRolesBAL _shiftBAL;
            private readonly ILogger<RolesController> _logger;


        public RolesController(IRolesBAL shiftBAL, ILogger<RolesController> logger)
            {
                _shiftBAL = shiftBAL;
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

            [HttpGet("list")]
            public async Task<IActionResult> Get()
                => Ok(await _shiftBAL.GetAll());
       
            [HttpGet("{id}")]
            public async Task<IActionResult> Get(Guid id)
                => Ok(await _shiftBAL.Get(id));
            
            
            [HttpPost]
            public async Task<IActionResult> Create(Roles r)
            {
                var user = GetUser();
                var id = await _shiftBAL.Create(r, user);
                return Ok(id);
            }
            

            [HttpPut("{id}")]
            public async Task<IActionResult> Update(Roles r)
            {
                var user = GetUser();
                return Ok(await _shiftBAL.Update(r, user));
            }
            

            [HttpDelete("{id}")]
            public async Task<IActionResult> Delete(Guid id)
            {
                var user = GetUser();
                return Ok(await _shiftBAL.Delete(id, user));
            }

            private Guid GetUser()
            {
                return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

    }
}
