using System;
using System.Collections.Generic;
using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SopDetailResponse
    {
        public Guid ID { get; set; }
        public string? SopTitle { get; set; }
        public string? SopDocument { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public SopStatus Status { get; set; }
        public int CurrentApprovalLevel { get; set; }
        public int DocumentVersion { get; set; }
        public string? Remark { get; set; }
        public DateTime? Created { get; set; }

        public List<SopApprovalHistoryResponse> ApprovalHistory { get; set; } = new();
        public List<SopVersionHistoryResponse> VersionHistory { get; set; } = new();
    }
}
