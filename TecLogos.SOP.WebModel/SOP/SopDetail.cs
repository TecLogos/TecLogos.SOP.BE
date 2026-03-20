using Microsoft.AspNetCore.Http;
using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.Base;

namespace TecLogos.SOP.WebModel.SOP
{
    public class CreateSopRequest
    {
        public string SopTitle { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string CommentText { get; set; }
        public IFormFile? DocumentFile { get; set; }
    }

    public class UpdateSopRequest
    {
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? CommentText { get; set; }
        public IFormFile? DocumentFile { get; set; }
    }

    public class SopActionRequest
    {
        public int NextApprovalLevel { get; set; }
        public string? Comments { get; set; }  // required on reject, optional on approve
    }

    public class SopDetailResponse : BaseModel
    {
        public string SopTitle { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string SopDocument { get; set; }
        public int SopDocumentVersion { get; set; }
        public string CommentText { get; set; }
        public int ApprovalLevel { get; set; }
        public int NextApprovalLevel { get; set; }
        public string StageName { get; set; }
        public string NextStageName { get; set; }
        public SopApprovalStatus? ApprovalStatus { get; set; }
        public string ApprovalStatusLabel => ApprovalStatus switch
        {
            SopApprovalStatus.Pending => "Pending",
            SopApprovalStatus.Approved => "Approved",
            SopApprovalStatus.Rejected => "Rejected",
            SopApprovalStatus.Completed => "Completed",
            SopApprovalStatus.Expired => "Expired",
            SopApprovalStatus.ReturnedForChanges => "Needs Changes",
            null => "Unknown",
            _ => "Unknown"
        };

        public List<SopApprovalHistoryResponse> SopApprovalHistoryResponseList { get; set; }
        public List<SopCommentsResponse> SopCommentsResponseList { get; set; }
    }

    public class SopApprovalHistoryResponse
    {
        public int ApprovalStatus { get; set; }
        public string StageName { get; set; }
        public string Comments { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    }

    public class SopCommentsResponse
    {
         
        public string CommentText { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    }

    public class SopListResponse
    {
        public int TotalCount { get; set; }
        public List<SopDetailResponse> Items { get; set; } = new();
    }

    public class SopApprovalHistoryListResponse
    {
        public int TotalCount { get; set; }
        public List<SopApprovalHistoryResponse> Items { get; set; } = new();
    }

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
            SopApprovalStatus.Completed => "Completed",
            SopApprovalStatus.Expired => "Expired",
            SopApprovalStatus.ReturnedForChanges => "Needs Changes",
            null => "Not Yet Reached",
            _ => "Unknown"
        };
    }

    public class SopTrackingResponse
    {
        public Guid SopId { get; set; }
        public List<SopTrackingStepResponse> Steps { get; set; } = new();
    }

    public class WorkFlowSetUpResponse
    {
        public Guid ID { get; set; }
        public string StageName { get; set; }
        public int ApprovalLevel { get; set; }   // 0–5
        public bool IsSupervisor { get; set; }
        public Guid? EmployeeGroupID { get; set; }   // NULL for supervisor stage
        public string? GroupName { get; set; }   // joined from EmployeeGroup.Name

        public string ApprovalLevelLabel => ApprovalLevel switch
        {
            0 => "Not Started",
            1 => "In Progress",
            2 => "Submitted",
            3 => "Level 1",
            4 => "Level 2",
            5 => "Level 3",
            _ => $"Level {ApprovalLevel}"
        };

        public string TypeLabel => IsSupervisor ? "Supervisor" : "Approver";
    }

    public class CreateWorkFlowStageRequest
    {
        public string StageName { get; set; }   // required
        public int ApprovalLevel { get; set; }   // 0–5
        public bool IsSupervisor { get; set; }
        public Guid? EmployeeGroupID { get; set; }   // required when IsSupervisor=false
    }

    public class UpdateWorkFlowStageRequest
    {
        public string? StageName { get; set; }
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid? EmployeeGroupID { get; set; }
    }
}