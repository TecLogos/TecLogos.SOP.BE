using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SetupWorkflowStageRequest
    {
        public string StageName { get; set; } = string.Empty;
        public int ApprovalLevel { get; set; }
        public bool IsSupervisor { get; set; }
        public Guid EmployeeGroupID { get; set; }
    }
}
