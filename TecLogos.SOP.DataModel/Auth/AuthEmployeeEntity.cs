
using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.Auth
{
    public class AuthEmployeeEntity : BaseModel
    {
        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;
        public int FailedLoginAttempts { get; set; }
        public DateTime? LastFailedLoginAttemptOn { get; set; }
        public DateTime? LastLoginDate { get; set; }

        public List<Role> Roles { get; set; } = new();
    }

}
