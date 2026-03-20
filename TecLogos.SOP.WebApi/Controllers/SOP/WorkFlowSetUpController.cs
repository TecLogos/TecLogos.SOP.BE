using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebModel.SOP;
using TecLogos.SOP.WebApi.Controllers.Base;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
 
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WorkFlowSetUpController : CustomControllerBase
    {
        private readonly IWorkFlowSetUpBAL _bal;
        private readonly ILogger<WorkFlowSetUpController> _logger;

        public WorkFlowSetUpController(IWorkFlowSetUpBAL bal, ILogger<WorkFlowSetUpController> logger)
        {
            _bal    = bal    ?? throw new ArgumentNullException(nameof(bal));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _bal.GetAll();
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _bal.GetById(id);
            if (result == null)
                return NotFound(new { success = false, message = "Workflow stage not found." });

            return Ok(new { success = true, data = result });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWorkFlowStageRequest request)
        {
            var userId = GetUserId();
            var id = await _bal.Create(request, userId);
            return Ok(new { success = true, id, message = "Workflow stage created successfully." });
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkCreate([FromBody] List<CreateWorkFlowStageRequest> requests)
        {
            if (requests == null || requests.Count == 0)
                return BadRequest(new { success = false, message = "At least one stage is required." });

            var userId = GetUserId();
            var ids = await _bal.BulkCreate(requests, userId);
            return Ok(new
            {
                success = true,
                ids,
                count = ids.Count,
                message = $"{ids.Count} workflow stage(s) created successfully."
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkFlowStageRequest request)
        {
            var userId = GetUserId();
            var updated = await _bal.Update(id, request, userId);

            if (!updated)
                return BadRequest(new { success = false, message = "Update failed. Stage may not exist." });

            return Ok(new { success = true, message = "Workflow stage updated successfully." });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetUserId();
            var deleted = await _bal.Delete(id, userId);

            if (!deleted)
                return NotFound(new { success = false, message = "Workflow stage not found or already deleted." });

            return Ok(new { success = true, message = "Workflow stage deleted successfully." });
        }

        private Guid GetUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(claim))
                throw new UnauthorizedAccessException("Invalid or missing token.");
            return Guid.Parse(claim);
        }
    }
}
