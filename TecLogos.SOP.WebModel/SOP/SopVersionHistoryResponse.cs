using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SopVersionHistoryResponse
    {
        public Guid ID { get; set; }
        public string? Name { get; set; }
        public string? FileName { get; set; }
        public int Status { get; set; }
        public int ApprovalLevel { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Remarks { get; set; }
        public DateTime? Created { get; set; }
    }
}
