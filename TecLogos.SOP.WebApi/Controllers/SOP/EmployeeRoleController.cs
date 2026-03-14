using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebApi.Controllers.Base;

namespace TecLogos.SOP.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EmployeeRoleController : CustomControllerBase
    {
        private readonly IEmployeeRoleBAL _BAL;

        public EmployeeRoleController(IEmployeeRoleBAL BAL)
        {
            _BAL = BAL;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetAll(
            Guid? employeeId,
            int? year,
            int? month)
        {
            var result = await _BAL.GetAll(employeeId, year, month);

            return Ok(new
            {
                TotalCount = result.Item1,
                Data = result.Item2
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> TrackRoleHistory(
           Guid? employeeId,
           int? year,
           int? month)
        {
            var result = await _BAL.TrackRoleHistory(employeeId, year, month);

            return Ok(new
            {
                TotalCount = result.Item1,
                Data = result.Item2
            });
        }
    }
}
