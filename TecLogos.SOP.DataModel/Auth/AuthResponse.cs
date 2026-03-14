
namespace TecLogos.SOP.DataModel.Auth
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public AuthUserDto? User { get; set; }
    }

}
