namespace TecLogos.SOP.EnumsAndConstants
{

    public enum SopApprovalStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Completed = 3,
        Expired = 4
    }


    public enum SopApprovalLevel
    {
        NotStarted = 0,
        InProgress = 1,
        Submitted = 2,
        Level1Approval = 3,
        Level2Approval = 4,
        Level3Approval = 5   // Max level — triggers Completed on approval
    }

    public enum SopDocumentStatus
    {
        Draft = 0,
        Active = 1,
        Superseded = 2,   // Replaced by a newer version
        Expired = 3    // ExpirationDate has passed
    }


    public enum EmployeeGroupType
    {
        Admin = 0,   // Creates SOPs — Not Started stage
        Initiator = 1,   // Edits SOPs   — In Progress stage
        Supervisor = 2,   // Reviews SOPs — Submitted stage (manager-based, no group check)
        ApproverL1 = 3,   // Level 1 approval
        ApproverL2 = 4,   // Level 2 approval
        ApproverL3 = 5    // Level 3 approval — highest authority
    }

    public enum WorkflowStageType
    {
        GroupReview = 0,   // IsSupervisor = 0 (Admin/Initiator/L1/L2/L3)
        SupervisorReview = 1    // IsSupervisor = 1 (Submitted stage only)
    }
}