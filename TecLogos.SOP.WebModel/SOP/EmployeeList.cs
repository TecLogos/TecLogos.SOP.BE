using TecLogos.SOP.WebModel.Base;

namespace TecLogos.SOP.WebModel.SOP
{
    public class EmployeeList : BaseModel
    {
        public string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
    }
}
