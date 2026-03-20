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
        private readonly IFileStorageBAL _fileStorage;
        private readonly ILogger<SopDetailController> _logger;

        public SopDetailController(
            ISopDetailBAL bal,
            IFileStorageBAL fileStorage,
            ILogger<SopDetailController> logger)
        {
            _bal = bal;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetAllSops(int? approvalStatus = null, int? year = null)
        {
            var result = await _bal.GetAllSops(approvalStatus, year);
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{sopId:guid}")]
        public async Task<IActionResult> GetSopById(Guid sopId)
        {
            var sop = await _bal.GetSopById(sopId);

            if (sop == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "SOP not found."
                });
            }

            return Ok(new
            {
                success = true,
                data = sop
            });
        }

        [HttpPost("create")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateSop([FromForm] CreateSopRequest request)
        {
            var userId = GetUserId();

            var sopId = Guid.NewGuid();

            string? storedPath = null;

            if (request.DocumentFile is { Length: > 0 })
            {
                var ext = Path.GetExtension(request.DocumentFile.FileName).ToLowerInvariant();

                if (ext != ".pdf")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Only PDF files are accepted."
                    });
                }

                storedPath = await _fileStorage.SaveSopDocumentAsync(
                    request.DocumentFile,
                    sopId,
                    version: 1
                );
            }

            var createdId = await _bal.CreateSop(request, userId, sopId, storedPath);

            return Ok(new
            {
                success = true,
                message = "SOP created successfully.",
                sopId = createdId,
                documentPath = storedPath
            });
        }


        [HttpPut("update/{sopId:guid}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateSop(Guid sopId, [FromForm] UpdateSopRequest request)
        {
            var userId = GetUserId();

            if (request.DocumentFile != null && request.DocumentFile.Length > 0)
            {
                var ext = Path.GetExtension(request.DocumentFile.FileName).ToLowerInvariant();

                if (ext != ".pdf")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Only PDF files are accepted."
                    });
                }
            }

            var ok = await _bal.UpdateSop(sopId, request, userId);

            if (!ok)
            {
                return NotFound(new
                {
                    success = false,
                    message = "SOP not found."
                });
            }

            return Ok(new
            {
                success = true,
                message = "SOP updated successfully."
            });
        }

        [HttpDelete("{sopId:guid}")]
        public async Task<IActionResult> DeleteSop(Guid sopId)
        {
            var userId = GetUserId();
            var ok = await _bal.DeleteSop(sopId, userId);
            if (!ok) return NotFound(new { success = false, message = "SOP not found." });
            return Ok(new { success = true, message = "SOP deleted successfully." });
        }

        [HttpGet("{sopId:guid}/document")]
        public async Task<IActionResult> DownloadDocument(Guid sopId)
        {
            try
            {
                var sop = await _bal.GetSopById(sopId);

                if (sop == null || string.IsNullOrWhiteSpace(sop.SopDocument))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Document not found."
                    });
                }

                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), sop.SopDocument);

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("File not found at path: {Path}", fullPath);

                    return NotFound(new
                    {
                        success = false,
                        message = "File does not exist on server."
                    });
                }

                var fileName = Path.GetFileName(fullPath);

                var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                _logger.LogInformation("Serving SOP document {File} for SopId={SopId}", fileName, sopId);

                return File(stream, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while downloading SOP document for SopId={SopId}", sopId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error while downloading file."
                });
            }
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