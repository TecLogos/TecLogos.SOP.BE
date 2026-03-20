using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.SOP
{
    public class SopDetail : BaseModel 
    {
        public string SopTitle { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string SopDocument { get; set; }
        public int SopDocumentVersion { get; set; } = 1;
        public string CommentText { get; set; }
        public int ApprovalLevel { get; set; } = 0;
        public int NextApprovalLevel { get; set; }
        public string StageName { get; set; }
        public string NextStageName { get; set; }
        public SopApprovalStatus? ApprovalStatus { get; set; }

        public List<SopApprovalHistory> SopApprovalHistoryList { get; set; } = [];
        public List<SopComments> SopCommentsList { get; set; } = [];

    }

    public class SopComments
    {
        public string CommentText { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    }
    public class SopApprovalHistory
    {
        public int ApprovalStatus { get; set; }
        public string StageName { get; set; }
        public string Comments { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
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