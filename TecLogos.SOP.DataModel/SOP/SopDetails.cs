using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.SOP
{
    
    // ── SopDetails ────────────────────────────────────────────────────────────
    public class SopDetails : BaseModel
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
    public class SopDetailsHistory : BaseModel
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
    public class SopDetailsWorkFlowSetUp : BaseModel
    {
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }

        // Joined
        public string? GroupName { get; set; }
    }

    // ── SopDetailsApprovalHistory ─────────────────────────────────────────────
    public class SopDetailsApprovalHistory : BaseModel
    {
        public Guid SopDetailsID { get; set; }
        public int ApprovalLevel { get; set; }
        public int ApprovalStatus { get; set; }
        public string? Comments { get; set; }
        public int ReferenceVersion { get; set; }
    }
}
