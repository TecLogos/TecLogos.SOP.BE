using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.Base;

namespace TecLogos.SOP.WebModel.SOP
{
    // ── POST: Create new SOP ──
    public class CreateSopRequest
    {
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }  // optional — leave null for evergreen
        public string? SopDocument { get; set; }  // optional — can upload later
        public string? Remark { get; set; }
    }

    // ── PUT: Approve or Reject ──
    public class SopActionRequest
    {
        public string? Comments { get; set; }  // required on reject, optional on approve
    }

    // ── Response: SOP list item ──
    // Used in: pending-list, my-history
    // Only carries columns that live in [SopDetails] + one joined Email
    public class SopDetailResponse : BaseModel
    {
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? SopDocument { get; set; }
        public int SopDocumentVersion { get; set; }
        public string? Remark { get; set; }
        public int ApprovalLevel { get; set; }
        public SopApprovalStatus? ApprovalStatus { get; set; }
        public string? ApprovalStatusLabel => ApprovalStatus switch
        {
            SopApprovalStatus.Pending => "Pending",
            SopApprovalStatus.Approved => "Approved",
            SopApprovalStatus.Rejected => "Rejected",
            _ => "Unknown"
        };

        // Joined from Employee — read-only context, not a DB column on SopDetails
        public string? CreatedByEmail { get; set; }
    }

    // ── Response: Approver history list item ──
    // Used in: approver-history
    // Carries columns from [SopDetailsApprovalHistory] joined with [SopDetails]
    public class SopApprovalHistoryResponse
    {
        public Guid SopDetailsID { get; set; }
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? SopDocument { get; set; }
        public string? Remark { get; set; }

        // From [SopDetailsApprovalHistory]
        public int ApprovalLevel { get; set; }
        public SopApprovalStatus ApprovalStatus { get; set; }
        public string? Comments { get; set; }
        public DateTime ActionedOn { get; set; }  // AH.Created — always set
        public string? ApprovalStatusLabel => ApprovalStatus switch
        {
            SopApprovalStatus.Approved => "Approved",
            SopApprovalStatus.Rejected => "Rejected",
            _ => "Pending"
        };
    }

    // ── Response: paginated SOP list ──
    public class SopListResponse
    {
        public int TotalCount { get; set; }
        public List<SopDetailResponse> Items { get; set; } = new();
    }

    // ── Response: paginated approver history list ──
    public class SopApprovalHistoryListResponse
    {
        public int TotalCount { get; set; }
        public List<SopApprovalHistoryResponse> Items { get; set; } = new();
    }

    // ── Response: single tracking step ──
    public class SopTrackingStepResponse
    {
        public Guid ID { get; set; }
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }

        // Null = stage not yet reached
        public SopApprovalStatus? ApprovalStatus { get; set; }
        public string? Comments { get; set; }
        public DateTime? ActionedOn { get; set; }
        public string? ActionedByEmail { get; set; }
        public string StageStatusLabel => ApprovalStatus switch
        {
            SopApprovalStatus.Approved => "Approved",
            SopApprovalStatus.Rejected => "Rejected",
            SopApprovalStatus.Pending => "Pending",
            _ => "Not Yet Reached"
        };
    }

    // ── Response: full tracking wrapper ──
    public class SopTrackingResponse
    {
        public Guid SopId { get; set; }
        public List<SopTrackingStepResponse> Steps { get; set; } = new();
    }
}