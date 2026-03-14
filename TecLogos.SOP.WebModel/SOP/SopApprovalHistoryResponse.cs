using System;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SopApprovalHistoryResponse
    {
        public Guid ID { get; set; }
        public int ApprovalLevel { get; set; }
        public int ApprovalStatus { get; set; }
        public string? Comments { get; set; }
        public DateTime? ActionDate { get; set; }
    }
}
