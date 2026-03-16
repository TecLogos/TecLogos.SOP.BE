using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.DataModel.SOP
{
    // ── Maps exactly to [SopDetails] table ──
    // No joined/computed fields here.
    // Queries that need extra context (email, comments)
    // use dedicated response models, not this DM.
    public class SopDetail
    {
        public Guid ID { get; set; } = Guid.NewGuid();
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }  // NULL = evergreen SOP (no expiry)
        public string? SopDocument { get; set; }  // NULL = document not yet uploaded
        public int SopDocumentVersion { get; set; } = 1;
        public string? Remark { get; set; }  // NULL = no remark provided
        public int ApprovalLevel { get; set; } = 0;
        public SopApprovalStatus? ApprovalStatus { get; set; }

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

    // ── Maps to [SopDetailsApprovalHistory] table ──
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

    // ── Used only for GetSopTracking dual-resultset merge ──
    // Combines SopDetailsWorkFlowSetUp (structure)
    // with SopDetailsApprovalHistory (what happened)
    public class SopTrackingStep
    {
        // From [SopDetailsWorkFlowSetUp]
        public Guid ID { get; set; }
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }

        // From [SopDetailsApprovalHistory] — null = stage not yet reached
        public SopApprovalStatus? ApprovalStatus { get; set; }
        public string? Comments { get; set; }
        public DateTime? ActionedOn { get; set; } // = AH.Created
        public string? ActionedByEmail { get; set; } // = joined Employee.Email
    }
}