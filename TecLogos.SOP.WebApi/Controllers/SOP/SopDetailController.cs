using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebModel.SOP;
using TecLogos.SOP.WebApi.Controllers.Base;

namespace TecLogos.SOP.API.Controllers.SOP
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
    public class SopDetailController : CustomControllerBase
    {
        private readonly ISopDetailBAL _bal;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<SopDetailController> _logger;

        public SopDetailController(
            ISopDetailBAL bal,
            IFileStorageService fileStorage,
            ILogger<SopDetailController> logger)
        {
            _bal = bal;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        // ── POST api/v1/sopdetail/create ──────────────────────────────────────
        // Accepts multipart/form-data.
        // Form fields: SopTitle, ExpirationDate (optional), Remark (optional)
        // Form file:   DocumentFile (PDF, optional at creation time)
        //
        // File saved to disk:
        //   {ProjectRoot}/Uploads/Sop-Detail/{sopId}/SopDocument/V1/{filename}
        //
        // Relative path stored in [SopDetails].[SopDocument]:
        //   "Uploads/Sop-Detail/{sopId}/SopDocument/V1/{filename}"
        [HttpPost("create")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateSop([FromForm] CreateSopRequest request)
        {
            var userId = GetUserId();

            // Step 1: Insert the DB record first — we need the new SOP ID for the file path
            var sopId = await _bal.CreateSop(request, userId);

            // Step 2: Save the uploaded file (if provided)
            string? storedPath = null;
            if (request.DocumentFile is { Length: > 0 })
            {
                // Only PDF allowed
                var ext = Path.GetExtension(request.DocumentFile.FileName).ToLowerInvariant();
                if (ext != ".pdf")
                    return BadRequest(new { success = false, message = "Only PDF files are accepted." });

                // Version 1 on first upload
                storedPath = await _fileStorage.SaveSopDocumentAsync(
                    request.DocumentFile, sopId, version: 1);

                // Step 3: Update [SopDetails].[SopDocument] with the stored path
                await _bal.UpdateSopDocument(sopId, storedPath);
            }

            _logger.LogInformation(
                "SOP created. ID={SopId} | Document={Path}", sopId, storedPath ?? "none");

            return Ok(new
            {
                success = true,
                message = "SOP created successfully.",
                sopId,
                documentPath = storedPath
            });
        }

        // ── GET api/v1/sopdetail/{sopId}/download ─────────────────────────────
        // Streams the stored PDF to the browser.
        // Supports inline viewing (Content-Disposition: inline) so browser PDF
        // viewer opens automatically when called from a frontend download button.
        [HttpGet("{sopId:guid}/download")]
        public async Task<IActionResult> DownloadDocument(Guid sopId)
        {
            var relativePath = await _bal.GetSopDocumentPath(sopId);

            if (string.IsNullOrWhiteSpace(relativePath))
                return NotFound(new { success = false, message = "No document has been uploaded for this SOP." });

            var absolutePath = _fileStorage.GetAbsolutePath(relativePath);

            if (!System.IO.File.Exists(absolutePath))
            {
                _logger.LogWarning("Document not found on disk: {Path}", absolutePath);
                return NotFound(new { success = false, message = "Document file not found on server." });
            }

            var fileName = Path.GetFileName(absolutePath);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(absolutePath);

            _logger.LogInformation("Serving document: {Path} ({Bytes} bytes)", absolutePath, fileBytes.Length);

            return File(fileBytes, "application/pdf", fileName, enableRangeProcessing: true);
        }

        // ── PUT api/v1/sopdetail/approve/{sopId} ──────────────────────────────
        [HttpPut("approve/{sopId:guid}")]
        public async Task<IActionResult> ApproveSop(Guid sopId, [FromBody] SopActionRequest request)
        {
            var userId = GetUserId();
            var result = await _bal.ApproveSop(sopId, userId, request.Comments);
            if (!result)
                return BadRequest(new
                {
                    success = false,
                    message = "Approval failed. You may not be authorised at this level, or the SOP is not in a Pending state."
                });
            return Ok(new { success = true, message = "SOP approved successfully." });
        }

        // ── PUT api/v1/sopdetail/reject/{sopId} ───────────────────────────────
        [HttpPut("reject/{sopId:guid}")]
        public async Task<IActionResult> RejectSop(Guid sopId, [FromBody] SopActionRequest request)
        {
            var userId = GetUserId();
            var result = await _bal.RejectSop(sopId, userId, request.Comments);
            if (!result)
                return BadRequest(new
                {
                    success = false,
                    message = "Rejection failed. The SOP may already be approved, completed, or rejected."
                });
            return Ok(new { success = true, message = "SOP rejected successfully." });
        }

        // ── GET api/v1/sopdetail/pending-list ─────────────────────────────────
        [HttpGet("pending-list")]
        public async Task<IActionResult> GetSopsForApproval(int? year = null)
        {
            var userId = GetUserId();
            var result = await _bal.GetSopsForApproval(userId, year);
            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/approver-history ─────────────────────────────
        [HttpGet("approver-history")]
        public async Task<IActionResult> GetSopsHistory(int approvalStatus = 0, int? year = null)
        {
            var userId = GetUserId();
            var result = await _bal.GetSopsHistory(userId, approvalStatus, year);
            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/my-history ───────────────────────────────────
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMySopsHistory(int approvalStatus = 0, int? year = null)
        {
            var userId = GetUserId();
            var result = await _bal.GetMySopsHistory(userId, approvalStatus, year);
            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/all ──────────────────────────────────────────
        [HttpGet("all")]
        public async Task<IActionResult> GetAllSops(int? approvalStatus = null, int? year = null)
        {
            var result = await _bal.GetAllSops(approvalStatus, year);
            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/is-approver ──────────────────────────────────
        [HttpGet("is-approver")]
        public async Task<IActionResult> IsApprover()
        {
            var userId = GetUserId();
            var result = await _bal.IsUserApprover(userId);
            return Ok(new { success = true, isApprover = result });
        }

        // ── GET api/v1/sopdetail/{sopId}/tracking ─────────────────────────────
        [HttpGet("{sopId:guid}/tracking")]
        public async Task<IActionResult> GetSopTracking(Guid sopId)
        {
            var result = await _bal.GetSopTracking(sopId);
            return Ok(new { success = true, data = result });
        }

        // ── PRIVATE ───────────────────────────────────────────────────────────
        private Guid GetUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(claim))
                throw new UnauthorizedAccessException("Invalid or missing token.");
            return Guid.Parse(claim);
        }
    }
}