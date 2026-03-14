using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class UpdateSopRequest
    {
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? Remark { get; set; }
    }
}
