using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TecLogos.SOP.DAL.Auth;
using TecLogos.SOP.DataModel.Auth;

namespace TecLogos.SOP.BAL.Auth
{
    public interface IAuthBAL
    {

        Task<AuthResponse> Login(Login login, string ipAddress);

        Task<AuthResponse> RefreshToken(string refreshToken, string ipAddress);

        Task<bool> RevokeToken(string token, string ipAddress);

        Task<bool> RevokeAllTokens(Guid employeeId, string ipAddress);

        Task<AuthEmployee?> GetUserProfile(Guid userId);

        string GenerateJwtToken(AuthEmployeeEntity employee);
    }

    public class AuthBAL : IAuthBAL
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthBAL> _logger;
        private readonly IAuthDAL _authDAL;

        private readonly string _jwtSecretKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly int _jwtExpirationMinutes;
        private readonly int _refreshTokenExpirationDays;

        public AuthBAL(
            IConfiguration configuration,
            ILogger<AuthBAL> logger,
            IAuthDAL authDAL)
        {
            _configuration = configuration;
            _logger = logger;
            _authDAL = authDAL;

            _jwtSecretKey = configuration["Jwt:Key"]
     ?? throw new InvalidOperationException("JWT Key missing");
            _jwtIssuer = configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("JWT Issuer missing");
            _jwtAudience = configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("JWT Audience missing");

            _jwtExpirationMinutes = configuration.GetValue("Jwt:ExpirationMinutes", 60);
            _refreshTokenExpirationDays = configuration.GetValue("Jwt:RefreshTokenExpirationDays", 7);
        }

        // ---------------- LOGIN ----------------
        public async Task<AuthResponse> Login(Login login, string ipAddress)
        {
            var employee = await _authDAL.GetEmployeeByEmail(login.Email)
                ?? throw new UnauthorizedAccessException("Invalid credentials");

            if (!employee.IsActive)
                throw new UnauthorizedAccessException("Account disabled");

            if (!string.IsNullOrEmpty(employee.PasswordHash) &&
                !BCrypt.Net.BCrypt.Verify(login.Password, employee.PasswordHash))
            {
                await _authDAL.IncrementFailedLoginAttempts(login.Email);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            employee.Roles = await _authDAL.GetEmployeeRoles(employee.ID);

            var jwtToken = GenerateJwtToken(employee);
            var refreshToken = GenerateRefreshToken();

            await _authDAL.SaveRefreshToken(
                employee.ID,
                refreshToken,
                ipAddress,
                _refreshTokenExpirationDays);

            await _authDAL.UpdateLastLogin(employee.ID);

            _logger.LogInformation("User logged in: {Email}", employee.Email);

            return new AuthResponse
            {
                Success = true,
                Token = jwtToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.Now.AddMinutes(_jwtExpirationMinutes),
                User = new AuthUserDto
                {
                    Id = employee.ID,
                    Email = employee.Email,
                    Roles = employee.Roles.Select(r => r.RoleName).ToList()
                }
            };
        }

        // ---------------- REFRESH TOKEN ----------------
        public async Task<AuthResponse> RefreshToken(string refreshToken, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new UnauthorizedAccessException("Refresh token missing");

            var storedToken = await _authDAL.GetRefreshToken(refreshToken)
                ?? throw new UnauthorizedAccessException("Invalid refresh token");

            var profile = await _authDAL.GetUserProfile(storedToken.EmployeeID)
                ?? throw new UnauthorizedAccessException("Employee not found");

            profile.Roles = await _authDAL.GetEmployeeRoles(profile.ID);

            var entity = new AuthEmployeeEntity
            {
                ID = profile.ID,
                Email = profile.Email,
                Roles = profile.Roles
            };

            var newJwt = GenerateJwtToken(entity);
            var newRefresh = GenerateRefreshToken();

            await _authDAL.RevokeRefreshToken(storedToken.ID, ipAddress);

            await _authDAL.SaveRefreshToken(
                profile.ID,
                newRefresh,
                ipAddress,
                _refreshTokenExpirationDays);

            return new AuthResponse
            {
                Success = true,
                Token = newJwt,
                RefreshToken = newRefresh,
                ExpiresAt = DateTime.Now.AddMinutes(_jwtExpirationMinutes),
                User = new AuthUserDto
                {
                    Id = profile.ID,
                    Email = profile.Email,
                    Roles = profile.Roles.Select(r => r.RoleName).ToList()
                }
            };
        }

        // ---------------- REVOKE ----------------
        public async Task<bool> RevokeToken(string token, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(token)) return true;

            var storedToken = await _authDAL.GetRefreshToken(token);

            if (storedToken == null)
            {
                _logger.LogWarning("Logout attempted with invalid or non-existent token: {Token}", token);
                return true;
            }

            await _authDAL.RevokeRefreshToken(storedToken.ID, ipAddress);
            return true;
        }
        public async Task<bool> RevokeAllTokens(Guid employeeId, string ipAddress)
        {
            var count = await _authDAL.RevokeAllTokensForEmployee(employeeId, ipAddress);
            return count > 0;
        }

        // ---------------- PROFILE ----------------
        public async Task<AuthEmployee?> GetUserProfile(Guid userId)
        {
            var profile = await _authDAL.GetUserProfile(userId);
            if (profile == null) return null;

            profile.Roles = await _authDAL.GetEmployeeRoles(userId);

            return profile;
        }

        // ---------------- HELPERS ----------------

        public string GenerateJwtToken(AuthEmployeeEntity employee)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSecretKey);

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, employee.ID.ToString()),
        new Claim(ClaimTypes.Email, employee.Email)
    };

            foreach (var role in employee.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.RoleName));
            }

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtExpirationMinutes),
                signingCredentials: credentials
            );

            return tokenHandler.WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }

}
