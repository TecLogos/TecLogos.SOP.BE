using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TecLogos.SOP.AuthBAL
{
    public interface IUserContextBAL
    {
        Guid UserID { get; }
        string Email { get; }
    }
    public class UserContextBAL : IUserContextBAL
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContextBAL(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid UserID
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?
                    .User?
                    .FindFirst(ClaimTypes.NameIdentifier)?
                    .Value;

                return Guid.TryParse(value, out var id) ? id : Guid.Empty;
            }
        }

        public string Email =>
            _httpContextAccessor.HttpContext?
                .User?
                .FindFirst(ClaimTypes.Email)?
                .Value ?? string.Empty;
    }
}
