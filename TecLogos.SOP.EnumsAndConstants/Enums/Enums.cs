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
    /// <summary>
    /// String/format constants used across the SOP module.
    /// Place this file in the TecLogos.SOP.EnumsAndConstants project.
    /// </summary>
    public static class SopConstants
    {
        /// <summary>
        /// Format string for the CSV export file name.
        /// Arg {0} = DateTime.UtcNow
        /// Example output: SOP_Export_20250314_153000.csv
        /// </summary>
        public const string ExcelExportFormat = "SOP_Export_{0:yyyyMMdd_HHmmss}.csv";

        /// <summary>
        /// Format string for the downloaded SOP PDF file name.
        /// Arg {0} = SOP name (spaces replaced with underscores)
        /// Arg {1} = DateTime.UtcNow
        /// Arg {2} = document version number
        /// Example output: MyProcedure_20250314_v3.pdf
        /// </summary>
        public const string SopFileNamingFormat = "{0}_{1:yyyyMMdd}_v{2}.pdf";
    }

    /// <summary>
    /// Approval status values used for SOP workflow state checks in the controller.
    /// Mirrors SopStatus but is used specifically for download-gate logic.
    /// </summary>
    public enum SopApprovalStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Submitted = 2,
        PendingApprovalLevel1 = 3,
        PendingApprovalLevel2 = 4,
        PendingApprovalLevel3 = 5,
        Completed = 6,
        Rejected = 7
    }