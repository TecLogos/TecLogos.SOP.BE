using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class CreateSopRequest
    {
        public string SopTitle { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; }
        public string? Remark { get; set; }
    }
}
