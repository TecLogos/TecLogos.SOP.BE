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
    public class RoleController : CustomControllerBase
    {
            private readonly IRoleBAL _roleBAL;
            private readonly ILogger<RoleController> _logger;


        public RoleController(IRoleBAL roleBAL, ILogger<RoleController> logger)
            {
                _roleBAL = roleBAL;
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

            [HttpGet("list")]
            public async Task<IActionResult> Get()
                => Ok(await _roleBAL.GetAll());
       
            [HttpGet("{id}")]
            public async Task<IActionResult> Get(Guid id)
                => Ok(await _roleBAL.Get(id));
            
            
            [HttpPost]
            public async Task<IActionResult> Create(Role r)
            {
                var user = GetUser();
                var id = await _roleBAL.Create(r, user);
                return Ok(id);
            }
            

            [HttpPut("{id}")]
            public async Task<IActionResult> Update(Role r)
            {
                var user = GetUser();
                return Ok(await _roleBAL.Update(r, user));
            }
            

            [HttpDelete("{id}")]
            public async Task<IActionResult> Delete(Guid id)
            {
                var user = GetUser();
                return Ok(await _roleBAL.Delete(id, user));
            }

            private Guid GetUser()
            {
                return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

    }
}
