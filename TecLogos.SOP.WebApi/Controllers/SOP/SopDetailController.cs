using iText.Svg;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.Common;
using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.SOP;   // ← was incorrectly "TecLogos.SOP.WebModel"

namespace TecLogos.SOP.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SopDetailsController : ControllerBase
    {
        private readonly ISopDetailBAL _sopBAL;
        private readonly IWebHostEnvironment _env;

        public SopDetailsController(ISopDetailBAL sopBAL, IWebHostEnvironment env)
        {
            _sopBAL = sopBAL;
            _env = env;
        }

        private Guid CurrentEmployeeID =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private string CurrentRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        // ── GET /api/SopDetails  (role-filtered list) ──────────────
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _sopBAL.GetSopsForRole(CurrentRole, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── GET /api/SopDetails/all  (Admin: includes expired) ─────
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllIncludingExpired()
        {
            var result = await _sopBAL.GetAll(true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── GET /api/SopDetails/{id} ───────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _sopBAL.GetById(id, CurrentEmployeeID, CurrentRole);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // ── POST /api/SopDetails/upload  (Admin) ───────────────────
        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Upload([FromForm] UploadSopRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (request.File == null || request.File.Length == 0)
                return BadRequest(ApiResponse<string>.Fail("A PDF file is required."));

            if (!request.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<string>.Fail("Only PDF files are accepted."));

            var savedFileName = await SaveFileAsync(request.File);
            var result = await _sopBAL.CreateSop(
                request.Name, savedFileName,
                request.ExpiryDate, request.Remarks,
                CurrentEmployeeID);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── PUT /api/SopDetails/update  (Admin) ────────────────────
        [HttpPut("update")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update([FromForm] UpdateSopRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            string? newFileName = null;
            if (request.File != null && request.File.Length > 0)
            {
                if (!request.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(ApiResponse<string>.Fail("Only PDF files are accepted."));
                newFileName = await SaveFileAsync(request.File);
            }

            var result = await _sopBAL.UpdateSop(request, newFileName, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── POST /api/SopDetails/submit  (Initiator) ───────────────
        [HttpPost("submit")]
        [Authorize(Roles = "Initiator")]
        public async Task<IActionResult> Submit([FromBody] SubmitSopRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _sopBAL.SubmitSop(request, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── POST /api/SopDetails/supervisor/forward  (Supervisor) ──
        [HttpPost("supervisor/forward")]
        [Authorize(Roles = "Supervisor")]
        public async Task<IActionResult> SupervisorForward([FromBody] SupervisorSubmitRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _sopBAL.SupervisorSubmitForApproval(request, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── POST /api/SopDetails/supervisor/requestchange ──────────
        [HttpPost("supervisor/requestchange")]
        [Authorize(Roles = "Supervisor")]
        public async Task<IActionResult> SupervisorRequestChange(
            [FromBody] SupervisorRequestChangeRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _sopBAL.SupervisorRequestChange(request, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── POST /api/SopDetails/approval  (Approver) ─────────────
        /// <remarks>Action: 1 = Approve, 2 = Reject</remarks>
        [HttpPost("approval")]
        [Authorize(Roles = "Approver")]
        public async Task<IActionResult> ProcessApproval([FromBody] ApprovalActionRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _sopBAL.ProcessApproval(request, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── GET /api/SopDetails/{id}/history ──────────────────────
        [HttpGet("{id:guid}/history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            var result = await _sopBAL.GetSopHistory(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── GET /api/SopDetails/export  (Admin) ────────────────────
        [HttpGet("export")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Export()
        {
            var result = await _sopBAL.GetExportData();
            if (!result.Success) return BadRequest(result);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("SOP_Id,SOP_Document_Name,Date_of_Expiry,Status_of_SOP_Submission");
            foreach (var row in result.Data)
                sb.AppendLine(
                    $"{row.SopId},{EscapeCsv(row.SopDocumentName)},{row.DateOfExpiry},{row.StatusOfSopSubmission}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = string.Format(SopConstants.ExcelExportFormat, DateTime.UtcNow);
            return File(bytes, "text/csv", fileName);
        }

        // ── Workflow Setup (Admin) ─────────────────────────────────
        [HttpGet("workflow")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetWorkflow()
        {
            var result = await _sopBAL.GetWorkFlowSetUp();
            return Ok(result);
        }

        [HttpPost("workflow")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateWorkflow([FromBody] WorkFlowSetUpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _sopBAL.SaveWorkFlowSetUp(request, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("workflow")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateWorkflow([FromBody] WorkFlowSetUpUpdateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _sopBAL.UpdateWorkFlowSetUp(request, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── DELETE /api/SopDetails/{id}  (Admin only) ──────────────
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _sopBAL.DeleteSop(id, CurrentEmployeeID);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── GET /api/SopDetails/{id}/download  (All authenticated) ──
        /// <remarks>Only Completed SOPs are available for download.</remarks>
        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var sopResult = await _sopBAL.GetById(id, CurrentEmployeeID, CurrentRole);
            if (!sopResult.Success || sopResult.Data == null)
                return NotFound(sopResult);

            var sop = sopResult.Data;

            // SopDetailResponse.ApprovalStatus is a computed int property → no enum cast needed
            if (sop.ApprovalStatus != (int)SopApprovalStatus.Completed)
                return StatusCode(403, ApiResponse<string>.Fail(
                    "Download is only available for Completed SOPs."));

            if (string.IsNullOrWhiteSpace(sop.FileName))
                return NotFound(ApiResponse<string>.Fail("No file associated with this SOP."));

            var uploadsPath = Path.Combine(_env.ContentRootPath, "Uploads", "SOPs");
            var filePath = Path.Combine(uploadsPath, sop.FileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound(ApiResponse<string>.Fail("File not found on server."));

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var downloadName = string.Format(
                SopConstants.SopFileNamingFormat,
                sop.Name?.Replace(" ", "_") ?? id.ToString(),
                DateTime.UtcNow,
                sop.SOPVersion);

            return File(fileBytes, "application/pdf", downloadName);
        }

        // ── Private helpers ────────────────────────────────────────
        private async Task<string> SaveFileAsync(IFormFile file)
        {
            var uploads = Path.Combine(_env.ContentRootPath, "Uploads", "SOPs");
            Directory.CreateDirectory(uploads);
            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var fullPath = Path.Combine(uploads, uniqueName);
            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return uniqueName;
        }

        private static string EscapeCsv(string? value) =>
            $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    }
}