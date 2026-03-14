namespace TecLogos.SOP.EnumsAndConstants
{
    public static class AppConstants
    {
        public const int MaxApprovalLevels = 3;
        public const int MaxFailedLoginAttempts = 5;

        public static class Roles
        {
            public const string Admin = "Admin";
            public const string Initiator = "Initiator";
            public const string Supervisor = "Supervisor";
            public const string Approver = "Approver";
        }
    }
}
