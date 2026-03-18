using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.Auth
{
    public class AuthEmployee : BaseModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;

        // Resolved from workflow setup + group membership (since many installs don't use Role claims)
        public string ResolvedRole { get; set; } = string.Empty; // Admin | Initiator | Supervisor | Approver

        // Convenience flags for UI (optional)
        public bool IsAdmin { get; set; }
        public bool IsInitiator { get; set; }
        public bool IsSupervisor { get; set; }
        public bool IsApprover { get; set; }
    }
}
