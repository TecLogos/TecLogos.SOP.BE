
namespace TecLogos.SOP.WebModel.SOP
{
    public class EmployeeRoleResponse
    {
        public Guid ID { get; set; }
        public Guid EmployeeID { get; set; }
        public string Email { get; set; }
        public string EmployeeName { get; set; } = "";
        public Guid RoleID { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }

    }
    public class RoleHistoryResponse
    {
        public Guid ID { get; set; }
        public Guid EmployeeID { get; set; }
        public string Email { get; set; }
        public string EmployeeName { get; set; } = "";
        public Guid RoleID { get; set; }
        public Guid OldRoleID { get; set; }
        public string RoleName { get; set; }
        public string OldRoleName { get; set; }
        public DateTime? ChangedOn { get; set; }
        public string? Remarks { get; set; }

    }
}
