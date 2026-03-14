
namespace TecLogos.SOP.DataModel.Auth
{
    public class AuthUserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

}
