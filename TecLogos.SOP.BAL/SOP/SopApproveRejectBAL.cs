using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface ISopApproveRejectBAL
    {
        Task<SopListResponse> GetSopsForApproval(Guid userId, int? year);
        Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments, int nextApprovalLevel);
        Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments);

    }

    public class SopApproveRejectBAL : ISopApproveRejectBAL
    {
        private readonly ISopApproveRejectDAL _dal;
        private readonly ILogger<SopApproveRejectBAL> _logger;

        public SopApproveRejectBAL(ISopApproveRejectDAL dal, ILogger<SopApproveRejectBAL> logger)
        {
            _dal = dal;
            _logger = logger;
        }

        public async Task<SopListResponse> GetSopsForApproval(Guid userId, int? year)
        {
            var (total, data) = await _dal.GetSopsForApproval(
                userId,
                approvalStatus: (int)SopApprovalStatus.Pending,
                year: year ?? 0);

            return new SopListResponse
            {
                TotalCount = total,
                Items = data.Select(SopDetailToResponse).ToList()
            };
        }

        public async Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments, int nextApprovalLevel)
        {
            if (sopId == Guid.Empty) throw new Exception("SOP ID is required.");
            if (approverId == Guid.Empty) throw new Exception("Approver ID is required.");

            _logger.LogInformation("BAL: Approving SOP {SopId} by {ApproverId}", sopId, approverId);
            return await _dal.ApproveSop(sopId, approverId, comments, nextApprovalLevel);
        }

        public async Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments)
        {
            if (sopId == Guid.Empty) throw new Exception("SOP ID is required.");
            if (approverId == Guid.Empty) throw new Exception("Approver ID is required.");

            if (string.IsNullOrWhiteSpace(comments))
                throw new Exception("Comments are required when rejecting a SOP.");

            _logger.LogInformation("BAL: Rejecting SOP {SopId} by {ApproverId}", sopId, approverId);
            return await _dal.RejectSop(sopId, approverId, comments);
        }

        private static SopDetailResponse SopDetailToResponse(DataModel.SOP.SopDetail d)  => new()
     {
         ID = d.ID,
         SopTitle = d.SopTitle,
         ExpirationDate = d.ExpirationDate,
         SopDocument = d.SopDocument,
         SopDocumentVersion = d.SopDocumentVersion,
         Remark = d.Remark,
         ApprovalLevel = d.ApprovalLevel,
         NextApprovalLevel = d.NextApprovalLevel,
            StageName = d.StageName,
            NextStageName = d.NextStageName,
            ApprovalStatus = d.ApprovalStatus

     };

    }
}
