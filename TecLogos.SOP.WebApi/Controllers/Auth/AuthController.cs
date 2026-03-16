using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TecLogos.SOP.WebApi.Controllers.Base;
using TecLogos.SOP.DataModel.Auth;
using TecLogos.SOP.DataModel.Base;
using TecLogos.SOP.BAL.Auth;

namespace TecLogos.SOP.WebApi.Controllers.Auth
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController: CustomControllerBase
    {
        private readonly IAuthBAL _authBAL;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthBAL authBAL, ILogger<AuthController> logger)
        {
            _authBAL = authBAL ?? throw new ArgumentNullException(nameof(authBAL));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] Login loginDto)
        {
            try
            {
                if (loginDto == null)
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Login request cannot be null"
                    });

                var ipAddress = GetClientIpAddress();
                var result = await _authBAL.Login(loginDto, ipAddress);

                if (result.Success)
                {
                    if (!string.IsNullOrWhiteSpace(result.RefreshToken))
                        SetRefreshTokenCookie(result.RefreshToken);

                    return Ok(result);
                }

                return Unauthorized(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized login attempt");
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed with unexpected error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Login failed. Please contact administrator."
                });
            }
        }

        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<ActionResult<AuthResponse>> RefreshToken()
        {
                var refreshToken = Request.Cookies["refreshToken"] ?? Request.Headers["X-Refresh-Token"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    _logger.LogWarning("Refresh token attempt without token");
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Refresh token is required"
                    });
                }

                var ipAddress = GetClientIpAddress();
                var result = await _authBAL.RefreshToken(refreshToken, ipAddress);

                if (result == null)
                {
                    _logger.LogError("AuthBAL.RefreshTokenAsync returned null");
                    return StatusCode(500, new AuthResponse
                    {
                        Success = false,
                        Message = "Authentication BAL error"
                    });
                }

                if (result.Success)
                {
                    if (!string.IsNullOrWhiteSpace(result.RefreshToken))
                    {
                        SetRefreshTokenCookie(result.RefreshToken);
                    }

                    _logger.LogInformation("Token refreshed successfully");
                    return Ok(result);
                }

                _logger.LogWarning("Token refresh failed - {Message}", result.Message);
                return Unauthorized(result);
        }

        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponse<bool>>> Logout()
        {
            string? refreshToken = Request.Cookies["refreshToken"];

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = Request.Headers["X-Refresh-Token"].FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                _logger.LogInformation("Logout called without RefreshToken. Auth Header present: {HasAuth}", !string.IsNullOrEmpty(authHeader));
            }
            else
            {
                var ipAddress = GetClientIpAddress();
                await _authBAL.RevokeToken(refreshToken, ipAddress);
            }

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true, 
                SameSite = SameSiteMode.Strict,
                Path = "/"   
            });

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Logged out successfully",
                Data = true
            });
        }

        [Authorize]
        [HttpPost("revoke-token")]
        public async Task<ActionResult<ApiResponse<bool>>> RevokeToken([FromBody] string? token = null)
        {
            // Get employee ID from JWT token claims
                var employeeIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (employeeIdClaim == null || !Guid.TryParse(employeeIdClaim.Value, out var employeeId))
                {
                    _logger.LogWarning("Revoke token attempt without valid employee ID");
                    return Unauthorized(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Invalid or missing employee ID in token",
                        Data = false
                    });
                }

                //  If specific token provided, revoke just that token
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var ipAddress = GetClientIpAddress();
                    var result = await _authBAL.RevokeToken(token, ipAddress);

                    if (result)
                    {
                        _logger.LogInformation("Token revoked for employee: {EmployeeId}", employeeId);
                        return Ok(new ApiResponse<bool>
                        {
                            Success = true,
                            Message = "Token revoked successfully",
                            Data = true
                        });
                    }
                    else
                    {
                        return BadRequest(new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Failed to revoke token",
                            Data = false
                        });
                    }
                }

                //  If no specific token, revoke ALL tokens for this employee (logout)
                else
                {
                    var ipAddress = GetClientIpAddress();
                    var result = await _authBAL.RevokeAllTokens(employeeId, ipAddress);

                    if (result)
                    {
                        _logger.LogInformation("All tokens revoked for employee: {EmployeeId}", employeeId);
                        
                        Response.Cookies.Delete("refreshToken");

                        return Ok(new ApiResponse<bool>
                        {
                            Success = true,
                            Message = "All tokens revoked successfully. Logged out.",
                            Data = true
                        });
                    }
                    else
                    {
                        return BadRequest(new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Failed to revoke tokens",
                            Data = false
                        });
                    }
                }
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<ActionResult<ApiResponse<bool>>> LogoutAll()
        {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return BadRequest(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Invalid user ID"
                    });
                }

                var ipAddress = GetClientIpAddress();
                var result = await _authBAL.RevokeAllTokens(userId, ipAddress);

                if (result)
                {

                    Response.Cookies.Delete("refreshToken", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/"
                    });

                    _logger.LogInformation("All tokens revoked for user: {UserId}", userId);
                    return Ok(new ApiResponse<bool>
                    {
                        Success = true,
                        Message = "Logged out from all devices successfully",
                        Data = true
                    });
                }

                return BadRequest(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Failed to logout from all devices"
                });
        }

        [Authorize]
        [HttpPost("validate-token")]
        public ActionResult<ApiResponse<bool>> ValidateToken()
        {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userNameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                var userRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrWhiteSpace(userIdClaim))
                {
                    return Unauthorized(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                _logger.LogInformation("Token validated for user: {UserId}", userIdClaim);
                return Ok(new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Token is valid",
                    Data = true
                });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<AuthEmployee>>> GetCurrentUser()
        {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<AuthEmployee> { Success = false, Message = "Invalid session" });
                }

                var fullProfile = await _authBAL.GetUserProfile(userId);

                if (fullProfile == null)
                    return NotFound(new ApiResponse<AuthEmployee> { Success = false, Message = "Profile not found" });

                return Ok(new ApiResponse<AuthEmployee>
                {
                    Success = true,
                    Message = "Full employee profile retrieved",
                    Data = fullProfile
                });
        }

        #region Private Helper Methods

        private string GetClientIpAddress()
        {
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                return Request.Headers["X-Forwarded-For"].ToString().Split(',')[0];

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.Now.AddDays(7),
                Path = "/"
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

        #endregion
    }
}