using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
    [Route("api/employee")]
    [Authorize]
    public class EmployeeController : BaseController
    {
        private readonly IEmployeeBAL _employeeBAL;
        private readonly IEmployeeDDLBAL _employeeDDLBAL;
        private readonly IEmployeeRoleBAL _employeeRoleBAL;
        private readonly IEmployeeGroupBAL _employeeGroupBAL;
        private readonly IEGDetailBAL _egDetailBAL;
        private readonly IRoleBAL _roleBAL;

        public EmployeeController(
            IEmployeeBAL employeeBAL,
            IEmployeeDDLBAL employeeDDLBAL,
            IEmployeeRoleBAL employeeRoleBAL,
            IEmployeeGroupBAL employeeGroupBAL,
            IEGDetailBAL egDetailBAL,
            IRoleBAL roleBAL)
        {
            _employeeBAL = employeeBAL;
            _employeeDDLBAL = employeeDDLBAL;
            _employeeRoleBAL = employeeRoleBAL;
            _employeeGroupBAL = employeeGroupBAL;
            _egDetailBAL = egDetailBAL;
            _roleBAL = roleBAL;
        }

        // ── EMPLOYEE CRUD ──────────────────────────────────────────────────────

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string term = "")
        {
            var (total, items) = await _employeeBAL.GetAll(pageNumber, pageSize, term);
            return Ok(new { TotalCount = total, Items = items });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var employee = await _employeeBAL.GetById(id);
            return employee == null ? NotFound() : Ok(employee);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Employee request)
        {
            var id = await _employeeBAL.Create(request, CurrentUserId);
            return Ok(new { ID = id, Message = "Employee created and onboarding invite sent." });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Employee request)
        {
            request.ID = id;
            var result = await _employeeBAL.Update(request, CurrentUserId);
            return result ? Ok(new { Message = "Employee updated." }) : Conflict(new { Message = "Concurrency conflict." });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _employeeBAL.Delete(id, CurrentUserId);
            return result ? Ok(new { Message = "Employee deleted." }) : NotFound();
        }

        // ── DDL ────────────────────────────────────────────────────────────────

        [HttpGet("ddl")]
        public async Task<IActionResult> GetDdl()
        {
            var result = await _employeeDDLBAL.GetAll();
            return Ok(result);
        }

        // ── ROLES ──────────────────────────────────────────────────────────────

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var result = await _roleBAL.GetAll();
            return Ok(result);
        }

        [HttpGet("roles/{id:guid}")]
        public async Task<IActionResult> GetRole(Guid id)
        {
            var result = await _roleBAL.Get(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateRole([FromBody] Role request)
        {
            var id = await _roleBAL.Create(request, CurrentUserId);
            return Ok(new { ID = id });
        }

        [HttpPut("roles/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRole(Guid id, [FromBody] Role request)
        {
            request.ID = id;
            var result = await _roleBAL.Update(request, CurrentUserId);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("roles/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteRole(Guid id)
        {
            var result = await _roleBAL.Delete(id, CurrentUserId);
            return result ? Ok() : NotFound();
        }

        // ── EMPLOYEE ROLES (assignments + history) ────────────────────────────

        [HttpGet("role-assignments")]
        public async Task<IActionResult> GetRoleAssignments(
            [FromQuery] Guid? employeeId,
            [FromQuery] int? year,
            [FromQuery] int? month)
        {
            var (count, data) = await _employeeRoleBAL.GetAll(employeeId, year, month);
            return Ok(new { TotalCount = count, Items = data });
        }

        [HttpGet("role-history")]
        public async Task<IActionResult> GetRoleHistory(
            [FromQuery] Guid? employeeId,
            [FromQuery] int? year,
            [FromQuery] int? month)
        {
            var (count, data) = await _employeeRoleBAL.TrackRoleHistory(employeeId, year, month);
            return Ok(new { TotalCount = count, Items = data });
        }

        // ── EMPLOYEE GROUPS ────────────────────────────────────────────────────

        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups()
        {
            var result = await _employeeGroupBAL.GetAll();
            return Ok(result);
        }

        [HttpGet("groups/{id:guid}")]
        public async Task<IActionResult> GetGroup(Guid id)
        {
            var result = await _employeeGroupBAL.Get(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("groups")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateGroup([FromBody] EmployeeGroup request)
        {
            var id = await _employeeGroupBAL.Create(request, CurrentUserId);
            return Ok(new { ID = id });
        }

        [HttpPut("groups/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] EmployeeGroup request)
        {
            request.ID = id;
            var result = await _employeeGroupBAL.Update(request, CurrentUserId);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("groups/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteGroup(Guid id)
        {
            var result = await _employeeGroupBAL.Delete(id, CurrentUserId);
            return result ? Ok() : NotFound();
        }

        // ── GROUP MEMBERS (EGDetail) ───────────────────────────────────────────

        [HttpGet("group-members")]
        public async Task<IActionResult> GetGroupMembers()
        {
            var result = await _egDetailBAL.GetAll();
            return Ok(result);
        }

        [HttpGet("group-members/{id:guid}")]
        public async Task<IActionResult> GetGroupMember(Guid id)
        {
            var result = await _egDetailBAL.Get(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("group-members")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddGroupMember([FromBody] EGDetail request)
        {
            var id = await _egDetailBAL.Create(request, CurrentUserId);
            return Ok(new { ID = id });
        }

        [HttpPut("group-members/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateGroupMember(Guid id, [FromBody] EGDetail request)
        {
            request.ID = id;
            var result = await _egDetailBAL.Update(request, CurrentUserId);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("group-members/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveGroupMember(Guid id)
        {
            var result = await _egDetailBAL.Delete(id, CurrentUserId);
            return result ? Ok() : NotFound();
        }
    }
}
