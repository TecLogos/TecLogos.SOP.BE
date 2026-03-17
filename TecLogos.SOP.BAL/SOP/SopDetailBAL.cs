using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface ISopDetailBAL
    {
        Task<Guid> CreateSop(CreateSopRequest request, Guid userId);
        Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments);
        Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments);
        Task<SopTrackingResponse> GetSopTracking(Guid sopId);
        Task<SopListResponse> GetSopsForApproval(Guid userId, int? year);
        Task<SopApprovalHistoryListResponse> GetSopsHistory(Guid userId, int approvalStatus, int? year);
        Task<SopListResponse> GetMySopsHistory(Guid userId, int approvalStatus, int? year);
        Task<SopListResponse> GetAllSops(int? approvalStatus, int? year);
        Task<bool> IsUserApprover(Guid userId);
        Task UpdateSopDocument(Guid sopId, string relativePath);
        Task<string?> GetSopDocumentPath(Guid sopId);
    }

    public class SopDetailBAL : ISopDetailBAL
    {
        private readonly ISopDetailDAL _dal;
        private readonly ILogger<SopDetailBAL> _logger;

        public SopDetailBAL(ISopDetailDAL dal, ILogger<SopDetailBAL> logger)
        {
            _dal = dal;
            _logger = logger;
        }

        // ── CREATE SOP ──
        public async Task<Guid> CreateSop(CreateSopRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.SopTitle))
                throw new Exception("SOP Title is required.");

            // ExpirationDate is optional (null = evergreen SOP)
            if (request.ExpirationDate.HasValue && request.ExpirationDate.Value <= DateTime.UtcNow)
                throw new Exception("Expiration date must be a future date.");

            var dm = new DataModel.SOP.SopDetail
            {
                ID = Guid.NewGuid(),
                SopTitle = request.SopTitle.Trim(),
                ExpirationDate = request.ExpirationDate,
                SopDocument = null,          // populated after file save via UpdateSopDocument()
                Remark = request.Remark,
                ApprovalLevel = 0,                        // Not Started
                ApprovalStatus = SopApprovalStatus.Pending,
                CreatedByID = userId,
                Created = DateTime.UtcNow
            };

            _logger.LogInformation("BAL: Creating SOP [{Title}] by {UserId}", dm.SopTitle, userId);
            return await _dal.CreateSop(dm);
        }

        // ── APPROVE SOP ──
        public async Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments)
        {
            if (sopId == Guid.Empty) throw new Exception("SOP ID is required.");
            if (approverId == Guid.Empty) throw new Exception("Approver ID is required.");

            _logger.LogInformation("BAL: Approving SOP {SopId} by {ApproverId}", sopId, approverId);
            return await _dal.ApproveSop(sopId, approverId, comments);
        }

        // ── REJECT SOP ──
        // Comments required on rejection — audit trail must explain why
        public async Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments)
        {
            if (sopId == Guid.Empty) throw new Exception("SOP ID is required.");
            if (approverId == Guid.Empty) throw new Exception("Approver ID is required.");

            if (string.IsNullOrWhiteSpace(comments))
                throw new Exception("Comments are required when rejecting a SOP.");

            _logger.LogInformation("BAL: Rejecting SOP {SopId} by {ApproverId}", sopId, approverId);
            return await _dal.RejectSop(sopId, approverId, comments);
        }

        // ── GET SOP TRACKING ──
        public async Task<SopTrackingResponse> GetSopTracking(Guid sopId)
        {
            if (sopId == Guid.Empty) throw new Exception("SOP ID is required.");

            var steps = await _dal.GetSopTracking(sopId);

            return new SopTrackingResponse
            {
                SopId = sopId,
                Steps = steps.Select(s => new SopTrackingStepResponse
                {
                    ID = s.ID,
                    StageName = s.StageName,
                    ApprovalLevel = s.ApprovalLevel,
                    IsSupervisor = s.IsSupervisor,
                    ApprovalStatus = s.ApprovalStatus,   // already SopApprovalStatus? — no cast
                    ActionedOn = s.ActionedOn,
                    ActionedByEmail = s.ActionedByEmail,
                    Comments = s.Comments
                }).ToList()
            };
        }

        // ── GET SOPs PENDING MY APPROVAL ──
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

        // ── GET SOPs I HAVE ACTIONED ──
        public async Task<SopApprovalHistoryListResponse> GetSopsHistory(
            Guid userId, int approvalStatus, int? year)
        {
            var (total, data) = await _dal.GetSopsHistory(userId, approvalStatus, year);

            return new SopApprovalHistoryListResponse
            {
                TotalCount = total,
                Items = data.Select(ApprovalHistoryToResponse).ToList()
            };
        }

        // ── GET SOPs I CREATED ──
        public async Task<SopListResponse> GetMySopsHistory(
            Guid userId, int approvalStatus, int? year)
        {
            var (total, data) = await _dal.GetMySopsHistory(userId, approvalStatus, year);

            return new SopListResponse
            {
                TotalCount = total,
                Items = data.Select(SopDetailToResponse).ToList()
            };
        }

        // ── GET ALL SOPs (admin/dashboard) ──
        public async Task<SopListResponse> GetAllSops(int? approvalStatus, int? year)
        {
            var (total, data) = await _dal.GetAllSops(approvalStatus, year);

            return new SopListResponse
            {
                TotalCount = total,
                Items = data.Select(SopDetailToResponse).ToList()
            };
        }

        // ── IS USER AN APPROVER ──
        public Task<bool> IsUserApprover(Guid userId)
            => _dal.IsUserApprover(userId);


        // ─────────────────────────────────────────────
        //  PRIVATE MAPPERS
        //  SopDetail DM      → SopDetailResponse WM
        //  SopApprovalHistory → SopApprovalHistoryResponse WM
        // ─────────────────────────────────────────────

        private static SopDetailResponse SopDetailToResponse(DataModel.SOP.SopDetail d)
            => new()
            {
                ID = d.ID,
                SopTitle = d.SopTitle,
                ExpirationDate = d.ExpirationDate,
                SopDocument = d.SopDocument,
                SopDocumentVersion = d.SopDocumentVersion,
                Remark = d.Remark,
                ApprovalLevel = d.ApprovalLevel,
                ApprovalStatus = d.ApprovalStatus,
                Version = d.Version,
                IsActive = d.IsActive,
                IsDeleted = d.IsDeleted,
                Created = d.Created,
                CreatedByID = d.CreatedByID,
                Modified = d.Modified,
                ModifiedByID = d.ModifiedByID
            };

        private static SopApprovalHistoryResponse ApprovalHistoryToResponse(
            DataModel.SOP.SopApprovalHistory ah)
            => new()
            {
                SopDetailsID = ah.SopDetailsID,
                ApprovalLevel = ah.ApprovalLevel,
                ApprovalStatus = ah.ApprovalStatus,
                Comments = ah.Comments,
                ActionedOn = ah.Created       // AH.Created = timestamp of the action
            };


        // ── UPDATE SOP DOCUMENT ───────────────────────────────────────────────
        public async Task UpdateSopDocument(Guid sopId, string relativePath)
        {
            await _dal.UpdateSopDocument(sopId, relativePath);
            _logger.LogInformation("BAL: SopDocument updated for {SopId} → {Path}", sopId, relativePath);
        }

        // ── GET SOP DOCUMENT PATH ─────────────────────────────────────────────
        public async Task<string?> GetSopDocumentPath(Guid sopId)
            => await _dal.GetSopDocumentPath(sopId);
    }
}