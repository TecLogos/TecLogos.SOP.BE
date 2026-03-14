using Microsoft.AspNetCore.Http;
using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.WebModel.SOP
{
    // ── REQUESTS ──────────────────────────────────────────────────────────────

    public class SopFilterRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public SopStatus? Status { get; set; }
    }

    /// <summary>Admin: upload a new SOP document (multipart/form-data).</summary>
    public class UploadSopRequest
    {
        public string Name { get; set; } = string.Empty;
        public IFormFile? File { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>Admin: update an existing SOP (multipart/form-data).</summary>
    public class UpdateSopRequest
    {
        public Guid SopDetailsID { get; set; }
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? Remark { get; set; }
        public IFormFile? File { get; set; }
    }

    /// <summary>Internal BAL helper used by CreateAsync.</summary>
    public class CreateSopRequest
    {
        public string SopTitle { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; }
        public string? Remark { get; set; }
    }

    /// <summary>Initiator submits an SOP for supervisor review.</summary>
    public class SubmitSopRequest
    {
        public Guid SopDetailsID { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>Supervisor forwards SOP to Level-1 approver.</summary>
    public class SupervisorSubmitRequest
    {
        public Guid SopDetailsID { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>Supervisor returns SOP to initiator for changes.</summary>
    public class SupervisorRequestChangeRequest
    {
        public Guid SopDetailsID { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>Internal BAL supervisor action — carries just the remarks.</summary>
    public class SupervisorActionRequest
    {
        public string? Remarks { get; set; }
    }

    /// <summary>Approver approves or rejects an SOP at the current level.</summary>
    public class ApprovalActionRequest
    {
        public Guid SopDetailsID { get; set; }
        public ApprovalStatus Action { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>Admin: create a new workflow stage.</summary>
    public class WorkFlowSetUpRequest
    {
        public string StageName { get; set; } = string.Empty;
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }
    }

    /// <summary>Admin: update an existing workflow stage.</summary>
    public class WorkFlowSetUpUpdateRequest
    {
        public Guid ID { get; set; }
        public string? StageName { get; set; }
        public int? ApprovalLevel { get; set; }
        public bool? IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }
    }

    /// <summary>Internal BAL alias for workflow stage creation.</summary>
    public class SetupWorkflowStageRequest
    {
        public string StageName { get; set; } = string.Empty;
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }
    }

    // ── AUTH REQUESTS ─────────────────────────────────────────────────────────

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RevokeTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CompleteOnboardingRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    // ── RESPONSES ─────────────────────────────────────────────────────────────

    public class SopListResponse
    {
        public Guid ID { get; set; }
        public string SopTitle { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; }
        public SopStatus Status { get; set; }
        public int CurrentApprovalLevel { get; set; }
        public int DocumentVersion { get; set; }
        public DateTime Created { get; set; }
    }

    public class SopDetailResponse
    {
        public Guid ID { get; set; }
        public string SopTitle { get; set; } = string.Empty;
        public string? SopDocument { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public SopStatus Status { get; set; }
        public int CurrentApprovalLevel { get; set; }
        public int DocumentVersion { get; set; }
        public string? Remark { get; set; }
        public DateTime Created { get; set; }
        public List<SopApprovalHistoryResponse> ApprovalHistory { get; set; } = new();
        public List<SopVersionHistoryResponse> VersionHistory { get; set; } = new();

        // ── Convenience properties used by the controller ──────────────────
        /// <summary>Raw int status — used in download endpoint check.</summary>
        public int ApprovalStatus => (int)Status;

        /// <summary>Alias for DocumentVersion — used in download file naming.</summary>
        public int SOPVersion => DocumentVersion;

        /// <summary>Alias for SopDocument — used in download endpoint.</summary>
        public string? FileName => SopDocument;

        /// <summary>Alias for SopTitle — used in download endpoint.</summary>
        public string? Name => SopTitle;
    }

    public class SopApprovalHistoryResponse
    {
        public Guid ID { get; set; }
        public int ApprovalLevel { get; set; }
        public int ApprovalStatus { get; set; }
        public string? Remarks { get; set; }
        public DateTime ActionDate { get; set; }
    }

    public class SopVersionHistoryResponse
    {
        public Guid ID { get; set; }
        public string? Name { get; set; }
        public string? FileName { get; set; }
        public int Status { get; set; }
        public int ApprovalLevel { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Remarks { get; set; }
        public DateTime Created { get; set; }
    }

    public class WorkflowStageResponse
    {
        public Guid ID { get; set; }
        public string StageName { get; set; } = string.Empty;
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }
        public string? GroupName { get; set; }
    }

    /// <summary>Row used for the CSV export endpoint.</summary>
    public class SopExportRow
    {
        public Guid SopId { get; set; }
        public string? SopDocumentName { get; set; }
        public string? DateOfExpiry { get; set; }
        public string? StatusOfSopSubmission { get; set; }
    }

    // ── AUTH RESPONSES ────────────────────────────────────────────────────────

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public Guid EmployeeID { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsFirstLogin { get; set; }
    }
}