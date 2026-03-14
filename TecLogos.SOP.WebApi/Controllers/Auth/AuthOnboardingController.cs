using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TecLogos.SOP.BAL.Auth;
using TecLogos.SOP.WebApi.Controllers.Base;

namespace TecLogos.SOP.WebApi.Controllers.Auth
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthOnboardingController : CustomControllerBase
    {
        private readonly IAuthOnboardingBAL _BAL;

        public AuthOnboardingController(IAuthOnboardingBAL BAL)
        {
            _BAL = BAL;
        }

        // ADMIN ONLY 
        [Authorize(Roles = "Admin")]
        [HttpPost("send-invite/{employeeId}")]
        public async Task<IActionResult> SendInvite(Guid employeeId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _BAL.SendInvite(employeeId, userId);
            return Ok("Invite sent successfully");
        }

        // PUBLIC 
        [AllowAnonymous]
        [HttpGet("validate")]
        public async Task<IActionResult> Validate(string token)
        {
            await _BAL.ValidateInvite(token);
            return Ok();
        }

        // PUBLIC 
        [AllowAnonymous]
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword(SetPasswordDto dto)
        {
            await _BAL.SetPassword(dto.Token, dto.Password);
            return Ok("Password created successfully");
        }

        // PUBLIC  (for success screen)
        [AllowAnonymous]
        [HttpGet("employee-info")]
        public async Task<IActionResult> GetEmployeeInfo(string token)
        {
            var data = await _BAL.GetEmployeeCredentials(token);
            return Ok(data);
        }

        public record SetPasswordDto(string Token, string Password);
    }
}
