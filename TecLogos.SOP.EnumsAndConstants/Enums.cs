namespace TecLogos.SOP.EnumsAndConstants
{
    public enum SopStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Submitted = 2,
        PendingApprovalLevel1 = 3,
        PendingApprovalLevel2 = 4,
        PendingApprovalLevel3 = 5,
        Rejected = 6,
        Completed = 7,
        Expired = 8
    }

    public enum ApprovalStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        NeedsChanges = 3
    }
}
