using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecLogos.SOP.AuthBAL;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebApi.Controllers.Base;
using TecLogos.SOP.WebModel.Base;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
    [Authorize (Roles="Admin")]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EmployeeController : CustomControllerBase

    {
        private readonly IEmployeeBAL _employeeBAL;
        private readonly ILogger<EmployeeController> _logger;
        private readonly IUserContextBAL _userContext;

        public EmployeeController(
            IEmployeeBAL employeeBAL,
            ILogger<EmployeeController> logger,
            IUserContextBAL userContext)
        {
            _employeeBAL = employeeBAL;
            _logger = logger;
            _userContext = userContext;
        }

        [HttpGet("list/{page:int}/{size:int}")]
        public async Task<IActionResult> GetAll(
            int page,
            int size,
            [FromQuery] string? term = null)
        {
            if (page <= 0 || size <= 0 || size > 100)
                return BadRequest(new { success = false, message = "Invalid pagination values" });

            var result = await _employeeBAL.GetAll(page, size, term);
            return Ok(new { success = true, data = result.Item2, count = result.Item1 });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var employee = await _employeeBAL.GetById(id);

            if (employee == null)
                return NotFound(new { success = false, message = "Employee not found" });

            return Ok(new { success = true, data = employee });
        }

        [HttpPost("new")]
        public async Task<IActionResult> Create([FromBody] WebModel.SOP.Employee employee)
        {
            var id = await _employeeBAL.Create(employee, _userContext.UserID);

            return Ok(new ResponseModel
            {
                Status = true,
                Message = "Employee created successfully",
                ID = id
            });
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] WebModel.SOP.Employee employee)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (employee.ID != Guid.Empty && employee.ID != id)
                return BadRequest(new { success = false, message = "ID mismatch" });

            employee.ID = id;
            await _employeeBAL.Update(employee, _userContext.UserID);

            return Ok(new ResponseModel
            {
                Status = true,
                Message = "Employee updated successfully",
                ID = id
            });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _employeeBAL.Delete(id, _userContext.UserID);

            if (!deleted)
                return NotFound(new { success = false, message = "Employee not found" });

            return Ok(new { success = true, message = "Employee deleted successfully" });
        }

    }
}