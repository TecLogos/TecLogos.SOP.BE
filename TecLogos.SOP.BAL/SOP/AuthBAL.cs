using TecLogos.SOP.Auth.Services;
using TecLogos.SOP.Common;
using TecLogos.SOP.Common.Helpers;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.SOP;
using Microsoft.Extensions.Logging;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IAuthBAL
    {
        Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string ipAddress);
        Task<ApiResponse<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress);
        Task<ApiResponse<bool>> RevokeTokenAsync(RevokeTokenRequest request, string ipAddress, Guid currentUserId);
        Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordRequest request, Guid currentUserId);
        Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request);
        Task<ApiResponse<bool>> CompleteOnboardingAsync(CompleteOnboardingRequest request);
    }

    public class AuthBAL : IAuthBAL
    {
        private readonly IAuthDAL _authDAL;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthBAL> _logger;

        public AuthBAL(IAuthDAL authDAL, IJwtService jwtService, ILogger<AuthBAL> logger)
        {
            _authDAL = authDAL;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string ipAddress)
        {
            // Explicit types to avoid 'Cannot infer type' errors
            (var employee, var auth, string? roleName) = await _authDAL.GetLoginDataAsync(request.Email);

            if (employee == null || auth == null)
                return ApiResponse<LoginResponse>.Fail("Invalid email or password.");

            if (!employee.IsActive || employee.IsDeleted)
                return ApiResponse<LoginResponse>.Fail("Account is inactive.");

            if (auth.IsLoginOnHold == true)
                return ApiResponse<LoginResponse>.Fail("Account is locked due to too many failed attempts. Contact admin.");

            if (!PasswordHasher.Verify(request.Password, auth.PasswordHash))
            {
                await _authDAL.UpdateAuthOnLoginAsync(employee.ID, false, employee.ID);
                var remaining = AppConstants.MaxFailedLoginAttempts - (auth.FailedLoginAttempts + 1);
                return ApiResponse<LoginResponse>.Fail(remaining <= 0
                    ? "Account locked after too many failed attempts."
                    : $"Invalid password. {remaining} attempt(s) remaining.");
            }

            await _authDAL.UpdateAuthOnLoginAsync(employee.ID, true, employee.ID);

            var accessToken = _jwtService.GenerateAccessToken(employee.ID, employee.Email, roleName ?? "Initiator");
            var refreshToken = PasswordHasher.GenerateToken();
            var expiresAt = DateTime.UtcNow.AddDays(7);

            await _authDAL.CreateRefreshTokenAsync(employee.ID, refreshToken, expiresAt, employee.ID);

            return ApiResponse<LoginResponse>.Ok(new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                EmployeeID = employee.ID,
                Email = employee.Email,
                Role = roleName ?? "Initiator",
                IsFirstLogin = auth.IsFirstLogin
            });
        }

        public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
        {
            // Explicit types to avoid 'Cannot infer type' errors
            (var rt, var employee, string? roleName) = await _authDAL.GetRefreshTokenAsync(request.RefreshToken);

            if (rt == null || employee == null)
                return ApiResponse<LoginResponse>.Fail("Invalid refresh token.");

            if (rt.RevokedAt != null)
                return ApiResponse<LoginResponse>.Fail("Refresh token has been revoked.");

            if (rt.ExpiresAt < DateTime.UtcNow)
                return ApiResponse<LoginResponse>.Fail("Refresh token has expired.");

            // Rotate: revoke old, issue new
            var newRefreshToken = PasswordHasher.GenerateToken();
            await _authDAL.RevokeRefreshTokenAsync(request.RefreshToken, newRefreshToken, ipAddress, employee.ID);
            await _authDAL.CreateRefreshTokenAsync(employee.ID, newRefreshToken, DateTime.UtcNow.AddDays(7), employee.ID);

            var accessToken = _jwtService.GenerateAccessToken(employee.ID, employee.Email, roleName ?? "Initiator");

            return ApiResponse<LoginResponse>.Ok(new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                EmployeeID = employee.ID,
                Email = employee.Email,
                Role = roleName ?? "Initiator",
                IsFirstLogin = false
            });
        }

        public async Task<ApiResponse<bool>> RevokeTokenAsync(RevokeTokenRequest request, string ipAddress, Guid currentUserId)
        {
            (var rt, var _, var __) = await _authDAL.GetRefreshTokenAsync(request.RefreshToken);
            if (rt == null) return ApiResponse<bool>.Fail("Token not found.");
            if (rt.RevokedAt != null) return ApiResponse<bool>.Fail("Token already revoked.");

            await _authDAL.RevokeRefreshTokenAsync(request.RefreshToken, null, ipAddress, currentUserId);
            return ApiResponse<bool>.Ok(true, "Token revoked successfully.");
        }

        public async Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordRequest request, Guid currentUserId)
        {
            var auth = await _authDAL.GetAuthByEmployeeIdAsync(currentUserId);
            if (auth == null) return ApiResponse<bool>.Fail("User not found.");

            if (!PasswordHasher.Verify(request.CurrentPassword, auth.PasswordHash))
                return ApiResponse<bool>.Fail("Current password is incorrect.");

            var newHash = PasswordHasher.Hash(request.NewPassword);
            await _authDAL.UpdatePasswordAsync(currentUserId, newHash, currentUserId);
            return ApiResponse<bool>.Ok(true, "Password changed successfully.");
        }

        public async Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            (var employee, var _, var __) = await _authDAL.GetLoginDataAsync(request.Email);
            if (employee == null) return ApiResponse<bool>.Ok(true, "If this email exists, a reset link has been sent.");

            var token = PasswordHasher.GenerateToken(32);
            var expires = DateTime.UtcNow.AddHours(2);
            await _authDAL.SetPasswordResetTokenAsync(employee.ID, token, expires, employee.ID);

            // TODO: Send email with reset token
            _logger.LogInformation("Password reset token for {Email}: {Token}", request.Email, token);

            return ApiResponse<bool>.Ok(true, "Password reset instructions sent.");
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var auth = await _authDAL.GetByPasswordResetTokenAsync(request.Token);
            if (auth == null) return ApiResponse<bool>.Fail("Invalid or expired reset token.");

            if (auth.PasswordResetTokenExpires < DateTime.UtcNow)
                return ApiResponse<bool>.Fail("Reset token has expired.");

            var newHash = PasswordHasher.Hash(request.NewPassword);
            await _authDAL.UpdatePasswordAsync(auth.EmployeeID, newHash, auth.EmployeeID);
            return ApiResponse<bool>.Ok(true, "Password reset successfully.");
        }

        public async Task<ApiResponse<bool>> CompleteOnboardingAsync(CompleteOnboardingRequest request)
        {
            var invite = await _authDAL.GetOnboardingInviteAsync(request.Token);
            if (invite == null) return ApiResponse<bool>.Fail("Invalid onboarding token.");
            if (invite.IsCompleted) return ApiResponse<bool>.Fail("Onboarding already completed.");
            if (invite.ExpiryDate < DateTime.UtcNow) return ApiResponse<bool>.Fail("Onboarding token has expired.");

            var newHash = PasswordHasher.Hash(request.NewPassword);
            await _authDAL.CompleteOnboardingAsync(invite.EmployeeID, newHash, invite.ID, invite.EmployeeID);
            return ApiResponse<bool>.Ok(true, "Account setup complete. You may now log in.");
        }
    }
}