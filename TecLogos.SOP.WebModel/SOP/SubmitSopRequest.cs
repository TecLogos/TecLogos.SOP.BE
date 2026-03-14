using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SubmitSopRequest
    {
        public Guid SopID { get; set; }
        public string? Comments { get; set; }
    }
}
