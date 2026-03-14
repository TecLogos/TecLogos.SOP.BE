using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
    [Route("api/auth")]
    public class AuthController : BaseController
    {
        private readonly IAuthBAL _authBAL;

        public AuthController(IAuthBAL authBAL)
        {
            _authBAL = authBAL;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authBAL.LoginAsync(request, IpAddress);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authBAL.RefreshTokenAsync(request, IpAddress);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            var result = await _authBAL.RevokeTokenAsync(request, IpAddress, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var result = await _authBAL.ChangePasswordAsync(request, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _authBAL.ForgotPasswordAsync(request);
            return Ok(result);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _authBAL.ResetPasswordAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("complete-onboarding")]
        [AllowAnonymous]
        public async Task<IActionResult> CompleteOnboarding([FromBody] CompleteOnboardingRequest request)
        {
            var result = await _authBAL.CompleteOnboardingAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
