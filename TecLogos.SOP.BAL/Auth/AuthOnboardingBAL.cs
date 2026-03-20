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
            var body = $@"
                         <html>
                         <body style='font-family: Arial, sans-serif; line-height:1.6; color:#333;'>
                         
                             <h2 style='color:#2c3e50;'>Welcome to TecLogos SOP 🎉</h2>
                         
                             <p>Hello,</p>
                         
                             <p>
                                 Your employee account has been successfully created in the <b>TecLogos SOP system</b>.
                             </p>
                         
                             <p>
                                 To activate your account and set your password, click the button below:
                             </p>
                         
                             <p style='text-align:center; margin: 20px 0;'>
                                 <a href='{link}' 
                                    style='background-color:#007bff; color:#fff; padding:12px 20px; 
                                           text-decoration:none; border-radius:5px; display:inline-block;'>
                                     Set Your Password
                                 </a>
                             </p>
                         
                             <p>
                                 Or copy and paste this link in your browser:
                                 <br/>
                                 <a href='{link}'>{link}</a>
                             </p>
                         
                             <p style='color:#e74c3c;'>
                                 ⚠ This link will expire in 48 hours for security reasons.
                             </p>
                         
                             <hr/>
                         
                             <p>
                                 After setting your password, you can log in using:
                                 <ul>
                                     <li>Your official email address</li>
                                     <li>The password you create during onboarding</li>
                                 </ul>
                             </p>
                         
                             <p>
                                 If you did not expect this email, please contact the HR team immediately.
                             </p>
                         
                             <p>
                                 We are excited to have you onboard and wish you a great journey with TecLogos!
                             </p>
                         
                             <br/>
                         
                             <p>
                                 Best regards,<br/>
                                 <b>SOP Team</b><br/>
                                 TecLogos
                             </p>
                         
                         </body>
                         </html>";


            await _emailBAL.SendEmailAsync(
                 email,
                 "Welcome to TecLogos SOP – Set Your Password",
                 body
             );
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
