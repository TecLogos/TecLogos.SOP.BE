using System;

namespace TecLogos.SOP.WebModel.SOP
{


    public class WorkFlowSetUpRequest
    {
        public string StageName { get; set; } = string.Empty;
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }
    }

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
}
