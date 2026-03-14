using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.Auth
{
    public class RefreshToken : BaseModel
    {
        public Guid EmployeeID { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

}
