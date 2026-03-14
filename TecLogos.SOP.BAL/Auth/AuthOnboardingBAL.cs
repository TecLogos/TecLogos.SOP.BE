using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using TecLogos.SOP.DAL.Auth;

namespace TecLogos.SOP.BAL.Auth
{

    public interface IAuthOnboardingBAL
    {
        Task SendInvite(Guid employeeId, Guid createdBy);
        Task<bool> ValidateInvite(string token);
        Task SetPassword(string token, string password);
        Task<object> GetEmployeeCredentials(string token);
    }
    public class AuthOnboardingBAL : IAuthOnboardingBAL
    {
        private readonly IAuthOnboardingDAL _repo;
        private readonly IPasswordHasherBAL _hasher;
        private readonly IEmailBAL _emailBAL;
        private readonly ILogger<AuthOnboardingBAL> _logger;

        public AuthOnboardingBAL(
            IAuthOnboardingDAL repo,
            IPasswordHasherBAL hasher,
            IEmailBAL emailBAL,
            ILogger<AuthOnboardingBAL> logger)
        {
            _repo = repo;
            _hasher = hasher;
            _emailBAL = emailBAL;
            _logger = logger;
        }

        // 1️⃣ SEND INVITE EMAIL
        public async Task SendInvite(Guid employeeId, Guid createdBy)
        {
            var token = await _repo.CreateOnboardingInvite(employeeId, createdBy);
            var email = await _repo.GetEmployeeEmail(employeeId);

            var link = $"http://localhost:5173/set-password?token={token}";

            await _emailBAL.SendEmailAsync(
                email,
                "Welcome to TecLogos SOP – Set Your Password",
                $@"
            Hello,
            
            Welcome to TecLogos! 🎉
            
            Your employee account has been successfully created in the TecLogos SOP.
            
            To activate your account and set your password, please click the secure link below:
            
            {link}
            
            This link will expire in 48 hours for security reasons.
            
            After setting your password, you can log in to the SOP using:
            • Your official email address
            • The password you create during onboarding
            
            If you did not expect this email, please contact the HR team immediately.
            
            We are excited to have you onboard and wish you a great journey with TecLogos!
            
            Best regards,  
            SOP Team  
            TecLogos
            ");

            _logger.LogInformation("Onboarding invite sent to {Email}", email);
        }

        // 2️⃣ VALIDATE TOKEN
        public async Task<bool> ValidateInvite(string token)
        {
            var result = await _repo.ValidateInviteToken(token);
            if (result.Error != null)
                throw new ArgumentException(result.Error);

            return true;
        }

        // 3️⃣ SET PASSWORD
        public async Task SetPassword(string token, string password)
        {
            var result = await _repo.ValidateInviteToken(token);

            if (result.Error != null)
                throw new ArgumentException(result.Error);

            var hash = _hasher.Hash(password);

            await _repo.CompleteOnboarding(result.EmployeeId!.Value, token, hash);
        }

        public async Task<object> GetEmployeeCredentials(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token is required");

            var data = await _repo.GetEmployeeByInviteToken(token);

            return new
            {
                Email = data.Email,
                Code = data.Code
            };
        }
    }
}
