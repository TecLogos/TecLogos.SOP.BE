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
    public class EmployeeGroupController: CustomControllerBase
    {
        private readonly IEmployeeGroupBAL _EmployeeGroupBAL;
        private readonly ILogger<EmployeeGroupController> _logger;

        public EmployeeGroupController(IEmployeeGroupBAL EmployeeGroupBAL, ILogger<EmployeeGroupController> logger)
        {
            _EmployeeGroupBAL = EmployeeGroupBAL ?? throw new ArgumentNullException(nameof(EmployeeGroupBAL));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("list")]
        public async Task<ActionResult<List<EmployeeGroup>>> GetAll()
        {
            var items = await _EmployeeGroupBAL.GetAll();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
               => Ok(await _EmployeeGroupBAL.Get(id));

        [HttpPost]
        public async Task<IActionResult> Create(EmployeeGroup lag)
        {
            var user = GetUser();
            var id = await _EmployeeGroupBAL.Create(lag, user);
            return Ok(id);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Update(EmployeeGroup lag)
        {
            var user = GetUser();
            return Ok(await _EmployeeGroupBAL.Update(lag, user));
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var user = GetUser();
            return Ok(await _EmployeeGroupBAL.Delete(id, user));
        }

        private Guid GetUser()
        {
            return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
