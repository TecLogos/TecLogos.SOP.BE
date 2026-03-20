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
    public class SopApproveRejectController : CustomControllerBase
    {
        private readonly ISopApproveRejectBAL _bal;
        private readonly IFileStorageBAL _fileStorage;
        private readonly ILogger<SopApproveRejectController> _logger;

        public SopApproveRejectController(
            ISopApproveRejectBAL bal,
            IFileStorageBAL fileStorage,
            ILogger<SopApproveRejectController> logger)
        {
            _bal = bal;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        [HttpPut("approve/{sopId:guid}")]
        public async Task<IActionResult> ApproveSop(Guid sopId, [FromBody] SopActionRequest request)
        {
            var userId = GetUserId();
            var result = await _bal.ApproveSop(sopId, userId, request.Comments, request.NextApprovalLevel);
            if (!result)
                return BadRequest(new
                {
                    success = false,
                    message = "Approval failed. You may not be authorised at this level, or the SOP is not in a Pending state."
                });
            return Ok(new { success = true, message = "SOP approved successfully." });
        }

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

        [HttpGet("pending-list")]
        public async Task<IActionResult> GetSopsForApproval(int? year = null)
        {
            var userId = GetUserId();
            var result = await _bal.GetSopsForApproval(userId, year);
            return Ok(new { success = true, data = result });
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