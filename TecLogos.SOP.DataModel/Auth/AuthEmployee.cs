using System.Data;
using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.Auth
{
    public class AuthEmployee : BaseModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;

        public List<Role> Roles { get; set; } = new();
    }
}
