using Microsoft.AspNetCore.Http;
using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class UpdateSopRequest
    {
        public Guid SopDetailsID { get; set; }
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? Remark { get; set; }
        public IFormFile? File { get; set; }
    }

    public class UploadSopRequest
    {
        public string Name { get; set; } = string.Empty;
        public IFormFile? File { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Remarks { get; set; }
    }

   
}
