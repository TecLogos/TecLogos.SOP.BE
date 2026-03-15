using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SubmitSopRequest
    {
        public Guid SopID { get; set; }         // Backend uses SopID (not SopDetailsID)
        public string? Comments { get; set; }   // Backend uses Comments (not Remarks)
    }

    public class SupervisorSubmitRequest
    {
        public Guid SopDetailsID { get; set; }
        public string? Comments { get; set; }
    }

    public class SupervisorRequestChangeRequest
    {
        public Guid SopDetailsID { get; set; }
        public string? Comments { get; set; }
    }





}
