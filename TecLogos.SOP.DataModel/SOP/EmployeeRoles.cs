
using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.SOP
{
    public class EmployeeRoles : BaseModel
    {
        public Guid EmployeeID { get; set; }

        public string Email { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public Guid RoleID { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
    }
    public class RoleHistory : BaseModel
    {
        public Guid EmployeeID { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public Guid RoleID { get; set; }
        public Guid OldRoleID { get; set; }
        public string RoleName { get; set; }
        public string OldRoleName { get; set; }
        public DateTime? ChangedOn { get; set; }
        public string? Remarks { get; set; }
    }
}
