using TecLogos.SOP.DataModel.Base;

namespace TecLogos.SOP.DataModel.SOP
{
    public class Employee : BaseModel
    {
        public string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
      
        public Guid? ManagerID { get; set; }
        public Guid? RoleID { get; set; }

    }

    public class EmployeeList : BaseModel
    {
        public string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
    }

    public class EmployeeGroup : BaseModel  
    {
        public string Name { get; set; }
    }
    public class EGDetail : BaseModel
    {
        public Guid EmployeeGroupID { get; set; }
        public Guid EmployeeID { get; set; }

        public string GroupName { get; set; }
        public string? EmployeeName { get; set; }   // ✅ nullable
        public string? Email { get; set; }          // ✅ nullable
    }

}
