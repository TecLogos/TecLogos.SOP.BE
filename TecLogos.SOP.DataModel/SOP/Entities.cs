namespace TecLogos.SOP.DataModel.SOP
{
    // ── Employee ──────────────────────────────────────────────────────────────
    public class Employee : BaseEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? Address { get; set; }

        // Joined from EmployeeRole (not a DB column on Employee)
        public Guid? RoleID { get; set; }
    }

    /// <summary>Lightweight list projection — no role join needed.</summary>
    public class EmployeeList
    {
        public Guid ID { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class EmployeeDDL
    {
        public Guid ID { get; set; }
        public string? Email { get; set; }
    }

    // ── Role ──────────────────────────────────────────────────────────────────
    public class Role : BaseEntity
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    // ── EmployeeRole ──────────────────────────────────────────────────────────
    public class EmployeeRole : BaseEntity
    {
        public Guid EmployeeID { get; set; }
        public Guid RoleID { get; set; }

        // Joined fields
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? RoleName { get; set; }
        public string? Description { get; set; }
    }

    // ── RoleHistory ───────────────────────────────────────────────────────────
    public class RoleHistory : BaseEntity
    {
        public Guid EmployeeID { get; set; }
        public Guid RoleID { get; set; }
        public Guid OldRoleID { get; set; }
        public DateTime ChangedOn { get; set; }
        public string? Remarks { get; set; }

        // Joined fields
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? RoleName { get; set; }
        public string? OldRoleName { get; set; }
        public DateTime OverridenOn { get; set; }
    }

    // ── AuthManager ───────────────────────────────────────────────────────────
    public class AuthManager : BaseEntity
    {
        public Guid EmployeeID { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpires { get; set; }
        public bool IsPasswordSet { get; set; }
        public bool IsFirstLogin { get; set; } = true;
        public bool? IsLoginOnHold { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LastFailedLoginAttemptOn { get; set; }
    }

    // ── RefreshToken ──────────────────────────────────────────────────────────
    public class RefreshToken : BaseEntity
    {
        public Guid EmployeeID { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedByIp { get; set; }
        public string? ReplacedToken { get; set; }
    }

    // ── OnboardingInvite ──────────────────────────────────────────────────────
    public class OnboardingInvite : BaseEntity
    {
        public Guid EmployeeID { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public bool IsCompleted { get; set; }
    }

    // ── EmployeeGroup ─────────────────────────────────────────────────────────
    public class EmployeeGroup : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    // ── EGDetail (EmployeeGroupDetail) ────────────────────────────────────────
    public class EGDetail : BaseEntity
    {
        public Guid EmployeeGroupID { get; set; }
        public Guid EmployeeID { get; set; }

        // Joined fields
        public string? GroupName { get; set; }
        public string? EmployeeName { get; set; }
        public string? Email { get; set; }
    }

    // ── SopDetails ────────────────────────────────────────────────────────────
    public class SopDetails : BaseEntity
    {
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? SopDocument { get; set; }
        public int SopDocumentVersion { get; set; } = 1;
        public string? Remark { get; set; }
        public int ApprovalLevel { get; set; }
        public int ApprovalStatus { get; set; }
    }

    // ── SopDetailsHistory ─────────────────────────────────────────────────────
    public class SopDetailsHistory : BaseEntity
    {
        public Guid SopDetailsID { get; set; }
        public string? Name { get; set; }
        public string? FileName { get; set; }
        public int ApprovalStatus { get; set; }
        public int ApprovalLevel { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Remarks { get; set; }
    }

    // ── SopDetailsWorkFlowSetUp ───────────────────────────────────────────────
    public class SopDetailsWorkFlowSetUp : BaseEntity
    {
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }

        // Joined
        public string? GroupName { get; set; }
    }

    // ── SopDetailsApprovalHistory ─────────────────────────────────────────────
    public class SopDetailsApprovalHistory : BaseEntity
    {
        public Guid SopDetailsID { get; set; }
        public int ApprovalLevel { get; set; }
        public int ApprovalStatus { get; set; }
        public string? Remarks { get; set; }
        public int ReferenceVersion { get; set; }
    }
}
