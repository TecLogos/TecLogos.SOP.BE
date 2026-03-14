using System;
using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SopListResponse
    {
        public Guid ID { get; set; }
        public string? SopTitle { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public SopStatus Status { get; set; }
        public int CurrentApprovalLevel { get; set; }
        public int DocumentVersion { get; set; }
        public DateTime? Created { get; set; }
    }
}
