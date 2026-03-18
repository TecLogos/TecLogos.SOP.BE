using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.SOP
{
    public class SopDetail : BaseModel 
    {
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? SopDocument { get; set; }
        public int SopDocumentVersion { get; set; } = 1;
        public string? Remark { get; set; }
        public int ApprovalLevel { get; set; } = 0;
        public int NextApprovalLevel { get; set; } 
        public SopApprovalStatus? ApprovalStatus { get; set; }

    }

    public class SopApprovalHistory : BaseModel
    {
        public Guid SopDetailsID { get; set; }
        public int ApprovalLevel { get; set; }
        public SopApprovalStatus ApprovalStatus { get; set; }
        public string? Comments { get; set; }   // NULL = approved without comment
        public int ReferenceVersion { get; set; }

    }
    public class SopTrackingStep : BaseModel
    {
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }

        // From [SopDetailsApprovalHistory] — null = stage not yet reached
        public SopApprovalStatus? ApprovalStatus { get; set; }
        public string? Comments { get; set; }
        public DateTime? ActionedOn { get; set; } // = AH.Created
        public string? ActionedByEmail { get; set; } // = joined Employee.Email
    }

    public class WorkFlowSetUp : BaseModel
    {
      
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        // NULL for supervisor stages (manager-based approval, no group check)
        public Guid? EmployeeGroupID { get; set; }

    }
}