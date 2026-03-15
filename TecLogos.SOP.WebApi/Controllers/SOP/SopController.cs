using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebApi.Controllers.Base;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SopController : CustomControllerBase
    {
        private readonly ISopDetailBAL _sopBAL;

        public SopController(ISopDetailBAL sopBAL)
        {
            _sopBAL = sopBAL;
        }

        // ── ADMIN ──────────────────────────────────────────────────────────────

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] SopFilterRequest filter)
        {
            var result = await _sopBAL.GetAllAsync(filter);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _sopBAL.GetByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(
            [FromForm] CreateSopRequest request,
            IFormFile document)
        {
            if (document == null || document.Length == 0)
                return BadRequest("Document file is required.");

            var result = await _sopBAL.CreateAsync(request, document, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromForm] UpdateSopRequest request,
            IFormFile? newDocument)
        {
            var result = await _sopBAL.UpdateAsync(id, request, newDocument, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _sopBAL.DeleteAsync(id, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("export")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportCsv()
        {
            var result = await _sopBAL.ExportCsvAsync();
            if (!result.Success) return BadRequest(result);
            return File(result.Data!, "text/csv", $"SOPs_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // ── WORKFLOW SETUP ─────────────────────────────────────────────────────

        [HttpGet("workflow-stages")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetWorkflowStages()
        {
            var result = await _sopBAL.GetWorkflowStagesAsync();
            return Ok(result);
        }

        [HttpPost("workflow-stages")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetupWorkflowStage([FromBody] SetupWorkflowStageRequest request)
        {
            var result = await _sopBAL.SetupWorkflowStageAsync(request, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("workflow-stages/bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkCreateWorkflowStages([FromBody] List<SetupWorkflowStageRequest> requests)
        {
            if (requests == null || !requests.Any())
                return BadRequest("At least one stage is required.");
            var result = await _sopBAL.BulkCreateWorkflowStagesAsync(requests, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("workflow-stages/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateWorkflowStage(Guid id, [FromBody] SetupWorkflowStageRequest request)
        {
            var result = await _sopBAL.UpdateWorkflowStageAsync(id, request, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("workflow-stages/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteWorkflowStage(Guid id)
        {
            var result = await _sopBAL.DeleteWorkflowStageAsync(id, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── INITIATOR ──────────────────────────────────────────────────────────

        [HttpGet("my-sops")]
        [Authorize(Roles = "Initiator")]
        public async Task<IActionResult> GetAvailableSops()
        {
            var result = await _sopBAL.GetAvailableForInitiatorAsync();
            return Ok(result);
        }

        [HttpPost("submit")]
        [Authorize(Roles = "Initiator")]
        public async Task<IActionResult> Submit([FromBody] SubmitSopRequest request)
        {
            var result = await _sopBAL.SubmitAsync(request, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── SUPERVISOR ─────────────────────────────────────────────────────────

        [HttpGet("supervisor/pending")]
        [Authorize(Roles = "Supervisor")]
        public async Task<IActionResult> GetPendingForSupervisor()
        {
            var result = await _sopBAL.GetPendingForSupervisorAsync();
            return Ok(result);
        }

        [HttpPost("{id:guid}/supervisor/forward")]
        [Authorize(Roles = "Supervisor")]
        public async Task<IActionResult> SupervisorForward(Guid id, [FromBody] SupervisorActionRequest request)
        {
            var result = await _sopBAL.SupervisorForwardAsync(id, request, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/supervisor/request-changes")]
        [Authorize(Roles = "Supervisor")]
        public async Task<IActionResult> SupervisorRequestChanges(Guid id, [FromBody] SupervisorActionRequest request)
        {
            var result = await _sopBAL.SupervisorRequestChangesAsync(id, request, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── APPROVER ───────────────────────────────────────────────────────────

        [HttpGet("approver/pending")]
        [Authorize(Roles = "Approver")]
        public async Task<IActionResult> GetPendingForApprover()
        {
            var result = await _sopBAL.GetPendingForApproverAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpPost("approve")]
        [Authorize(Roles = "Approver")]
        public async Task<IActionResult> ProcessApproval([FromBody] ApprovalActionRequest request)
        {
            var result = await _sopBAL.ProcessApprovalAsync(request, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── PDF DOWNLOAD ───────────────────────────────────────────────────────

        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var result = await _sopBAL.DownloadAsync(id);
            if (!result.Success) return BadRequest(result);
            return File(result.Data!, "application/pdf", $"SOP_{id}.pdf");
        }
    }
}