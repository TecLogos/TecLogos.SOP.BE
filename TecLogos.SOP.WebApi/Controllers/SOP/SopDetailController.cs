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
        private readonly ILogger<SopDetailController> _logger;

        public SopDetailController(ISopDetailBAL bal, ILogger<SopDetailController> logger)
        {
            _bal = bal;
            _logger = logger;
        }

        // ── POST api/v1/sopdetail/create ──
        [HttpPost("create")]
        public async Task<IActionResult> CreateSop([FromBody] CreateSopRequest request)
        {
            var userId = GetUserId();
            var id = await _bal.CreateSop(request, userId);

            return Ok(new { success = true, message = "SOP created successfully.", sopId = id });
        }

        // ── PUT api/v1/sopdetail/approve/{sopId} ──
        [HttpPut("approve/{sopId:guid}")]
        public async Task<IActionResult> ApproveSop(
            Guid sopId,
            [FromBody] SopActionRequest request)
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

        // ── PUT api/v1/sopdetail/reject/{sopId} ──
        [HttpPut("reject/{sopId:guid}")]
        public async Task<IActionResult> RejectSop(
            Guid sopId,
            [FromBody] SopActionRequest request)
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

        // ── GET api/v1/sopdetail/pending-list ──
        // Returns SOPs where the logged user is the next required actor
        [HttpGet("pending-list")]
        public async Task<IActionResult> GetSopsForApproval(int? year = null)
        {
            var userId = GetUserId();
            var result = await _bal.GetSopsForApproval(userId, year);

            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/approver-history ──
        // Returns actions the logged user has taken (approved/rejected rows)
        [HttpGet("approver-history")]
        public async Task<IActionResult> GetSopsHistory(
            int approvalStatus = 0,
            int? year = null)
        {
            var userId = GetUserId();
            var result = await _bal.GetSopsHistory(userId, approvalStatus, year);

            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/my-history ──
        // Returns SOPs created by the logged user
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMySopsHistory(
            int approvalStatus = 0,
            int? year = null)
        {
            var userId = GetUserId();
            var result = await _bal.GetMySopsHistory(userId, approvalStatus, year);

            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/all ──
        // Admin/dashboard: all SOPs, optionally filtered
        // approvalStatus: null=all 0=Pending 1=Approved 2=Rejected 3=Completed 4=Expired
        [HttpGet("all")]
        public async Task<IActionResult> GetAllSops(
            int? approvalStatus = null,
            int? year = null)
        {
            var result = await _bal.GetAllSops(approvalStatus, year);
            return Ok(new { success = true, data = result });
        }

        // ── GET api/v1/sopdetail/is-approver ──
        [HttpGet("is-approver")]
        public async Task<IActionResult> IsApprover()
        {
            var userId = GetUserId();
            var result = await _bal.IsUserApprover(userId);

            return Ok(new { success = true, isApprover = result });
        }

        // ── GET api/v1/sopdetail/{sopId}/tracking ──
        // Returns all 6 workflow stages merged with actioned history
        [HttpGet("{sopId:guid}/tracking")]
        public async Task<IActionResult> GetSopTracking(Guid sopId)
        {
            var result = await _bal.GetSopTracking(sopId);
            return Ok(new { success = true, data = result });
        }

        // ── PRIVATE ──
        private Guid GetUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(claim))
                throw new UnauthorizedAccessException("Invalid or missing token.");
            return Guid.Parse(claim);
        }
    }
}