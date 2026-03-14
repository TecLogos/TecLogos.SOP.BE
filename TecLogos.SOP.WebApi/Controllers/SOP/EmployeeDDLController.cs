using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecLogos.SOP.WebApi.Controllers.Base;
using TecLogos.SOP.BAL.SOP;
using Microsoft.AspNetCore.Authorization;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
    [Authorize]
    [Route("api/v1/[controller]")]
    [ApiController]
    public class EmployeeDDLController : CustomControllerBase
    {
        private readonly IEmployeeDDLBAL _EmployeeDDLBAL;
        private readonly ILogger<EmployeeDDLController> _logger;

        public EmployeeDDLController(IEmployeeDDLBAL EmployeeDDLBAL, ILogger<EmployeeDDLController> logger)
        {
            _EmployeeDDLBAL = EmployeeDDLBAL ?? throw new ArgumentNullException(nameof(EmployeeDDLBAL));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("list")]
        public async Task<ActionResult<List<TecLogos.SOP.WebModel.SOP.EmployeeDDL>>> GetAll()
        {
            var items = await _EmployeeDDLBAL.GetAll();
            return Ok(items);
        }
    }
}
