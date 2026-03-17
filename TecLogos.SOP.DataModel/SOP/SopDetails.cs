using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.DataModel.SOP
{
    public class SopDetail
    {
        public Guid ID { get; set; } = Guid.NewGuid();
        public string? SopTitle { get; set; }
        public DateTime ExpirationDate { get; set; }  // NULL = evergreen SOP (no expiry)
        public string? SopDocument { get; set; }  // NULL = document not yet uploaded
        public int SopDocumentVersion { get; set; } = 1;
        public string? Remark { get; set; }  // NULL = no remark provided
        public int ApprovalLevel { get; set; } = 0;
        public SopApprovalStatus ApprovalStatus { get; set; }

        // ── Audit columns (match DB exactly) ──
        public int Version { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public Guid CreatedByID { get; set; }
        public DateTime? Modified { get; set; }
        public Guid? ModifiedByID { get; set; }
        public DateTime? Deleted { get; set; }
        public Guid? DeletedByID { get; set; }
    }

    public class SopApprovalHistory
    {
        public Guid ID { get; set; }
        public Guid SopDetailsID { get; set; }
        public int ApprovalLevel { get; set; }
        public SopApprovalStatus ApprovalStatus { get; set; }
        public string? Comments { get; set; }   // NULL = approved without comment
        public int ReferenceVersion { get; set; }

        // ── Audit columns ──
        public int Version { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime Created { get; set; }
        public Guid CreatedByID { get; set; }
        public DateTime? Modified { get; set; }
        public Guid? ModifiedByID { get; set; }
        public DateTime? Deleted { get; set; }
        public Guid? DeletedByID { get; set; }
    }
    public class SopTrackingStep
    {
        // From [SopDetailsWorkFlowSetUp]
        public Guid ID { get; set; }
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }

        // From [SopDetailsApprovalHistory] — null = stage not yet reached
        public SopApprovalStatus ApprovalStatus { get; set; }
        public string? Comments { get; set; }
        public DateTime? ActionedOn { get; set; } // = AH.Created
        public string? ActionedByEmail { get; set; } // = joined Employee.Email
    }

    public class WorkFlowSetUp
    {
        public Guid ID { get; set; }
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }   

        // Audit columns (from BaseModel pattern)
        public int Version { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime Created { get; set; }
        public Guid CreatedByID { get; set; }
        public DateTime? Modified { get; set; }
        public Guid? ModifiedByID { get; set; }
    }
}