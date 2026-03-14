using TecLogos.SOP.EnumsAndConstants;
using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class ApprovalActionRequest
    {
        public Guid SopID { get; set; }
        public ApprovalStatus Action { get; set; }
        public string? Comments { get; set; }
    }
}
