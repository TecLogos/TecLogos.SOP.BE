
using TecLogos.SOP.WebModel.Base;

namespace TecLogos.SOP.WebModel.SOP
{
    // ── Employee ───────────────────────────────────────────────────
    public class EmployeeResponse
    {
        public Guid ID { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
        public string Address { get; set; }
        public bool IsActive { get; set; }
        public DateTime Created { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class CreateEmployeeRequest
    {
        
      
        public string FirstName { get; set; }

      
        public string MiddleName { get; set; }

        
      
        public string LastName { get; set; }

        
        
        public string Email { get; set; }

        
        
        public string MobileNumber { get; set; }

        public string Address { get; set; }

        
        public Guid RoleID { get; set; }
    }

    public class UpdateEmployeeRequest
    {
        
        public Guid ID { get; set; }

        
      
        public string FirstName { get; set; }

      
        public string MiddleName { get; set; }

        
      
        public string LastName { get; set; }

        
        
        public string Email { get; set; }

        
        
        public string MobileNumber { get; set; }

        public string Address { get; set; }
    }

    // ── Role ───────────────────────────────────────────────────────
    public class RoleResponse
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class AssignRoleRequest
    {
        
        public Guid EmployeeID { get; set; }

        
        public Guid RoleID { get; set; }
    }

    // ── Auth ───────────────────────────────────────────────────────
   

    public class SetPasswordRequest
    {
        
        public string Token { get; set; }

        
    
        public string NewPassword { get; set; }

        
      
        public string ConfirmPassword { get; set; }
    }


    // ── Employee Group ─────────────────────────────────────────────
    public class EmployeeGroupResponse
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public int MemberCount { get; set; }
        public List<EmployeeResponse> Members { get; set; }
    }

    public class CreateGroupRequest
    {
        
     
        public string Name { get; set; }

        public List<Guid> EmployeeIDs { get; set; } = new List<Guid>();
    }

    public class UpdateGroupRequest
    {
        
        public Guid ID { get; set; }

 
        public string Name { get; set; }
    }

    public class AddGroupMemberRequest
    {
        
        public Guid EmployeeGroupID { get; set; }

        
        public Guid EmployeeID { get; set; }
    }

    public class Employee : BaseModel
    {
        public string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
        public Guid? RoleID { get; set; }

    }

    public class EmployeeGroup : BaseModel
    {
        public string Name { get; set; }
    }

    public class EGDetail : BaseModel
    {
        public Guid EmployeeGroupID { get; set; }
        public Guid EmployeeID { get; set; }

        public string? GroupName { get; set; }

        public string? EmployeeCode { get; set; }   // ✅ nullable
        public string? EmployeeName { get; set; }   // ✅ nullable
        public string? Email { get; set; }          // ✅ nullable
    }

    public class EmployeeList : BaseModel
    {
        public string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
       
    }

}