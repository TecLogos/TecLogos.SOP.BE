using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class WorkflowStageResponse
    {
        public Guid ID { get; set; }
        public string StageName { get; set; } = string.Empty;
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }
        public string? GroupName { get; set; }
    }
}
