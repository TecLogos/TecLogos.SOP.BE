using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.Common;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.DataModel.SOP;
using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface ISopDetailBAL
    {
        // Admin
        Task<ApiResponse<PagedResult<SopListResponse>>> GetAllAsync(SopFilterRequest filter);
        Task<ApiResponse<SopDetailResponse>> GetByIdAsync(Guid id);
        Task<ApiResponse<SopDetailResponse>> CreateAsync(CreateSopRequest request, IFormFile document, Guid createdBy);
        Task<ApiResponse<SopDetailResponse>> UpdateAsync(Guid id, UpdateSopRequest request, IFormFile? newDocument, Guid modifiedBy);
        Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid deletedBy);
        Task<ApiResponse<byte[]>> ExportCsvAsync();

        // Initiator
        Task<ApiResponse<List<SopListResponse>>> GetAvailableForInitiatorAsync();
        Task<ApiResponse<bool>> SubmitAsync(SubmitSopRequest request, Guid submittedBy);

        // Supervisor
        Task<ApiResponse<List<SopListResponse>>> GetPendingForSupervisorAsync();
        Task<ApiResponse<bool>> SupervisorForwardAsync(Guid sopId, SupervisorActionRequest request, Guid supervisorId);
        Task<ApiResponse<bool>> SupervisorRequestChangesAsync(Guid sopId, SupervisorActionRequest request, Guid supervisorId);

        // Approver
        Task<ApiResponse<List<SopListResponse>>> GetPendingForApproverAsync(Guid approverId);
        Task<ApiResponse<bool>> ProcessApprovalAsync(ApprovalActionRequest request, Guid approverId);

        // PDF
        Task<ApiResponse<byte[]>> DownloadAsync(Guid id);

        // Workflow Setup
        Task<ApiResponse<List<WorkflowStageResponse>>> GetWorkflowStagesAsync();
        Task<ApiResponse<WorkflowStageResponse>> SetupWorkflowStageAsync(SetupWorkflowStageRequest request, Guid createdBy);
        Task<ApiResponse<List<WorkflowStageResponse>>> BulkCreateWorkflowStagesAsync(List<SetupWorkflowStageRequest> requests, Guid createdBy);
        Task<ApiResponse<WorkflowStageResponse>> UpdateWorkflowStageAsync(Guid id, SetupWorkflowStageRequest request, Guid modifiedBy);
        Task<ApiResponse<bool>> DeleteWorkflowStageAsync(Guid id, Guid deletedBy);
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

        // ── ADMIN ──────────────────────────────────────────────────────────────

        public async Task<ApiResponse<PagedResult<SopListResponse>>> GetAllAsync(SopFilterRequest filter)
        {
            var (total, items) = await _dal.GetAllAsync(
                filter.PageNumber, filter.PageSize, filter.Search, (int?)filter.Status);

            return ApiResponse<PagedResult<SopListResponse>>.Ok(new PagedResult<SopListResponse>
            {
                Items = items.Select(MapToListResponse).ToList(),
                TotalCount = total,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            });
        }

        public async Task<ApiResponse<SopDetailResponse>> GetByIdAsync(Guid id)
        {
            var sop = await _dal.GetByIdAsync(id);
            if (sop == null) return ApiResponse<SopDetailResponse>.Fail("SOP not found.");

            var approvalHistory = await _dal.GetApprovalHistoryAsync(id);
            var versionHistory = await _dal.GetVersionHistoryAsync(id);

            return ApiResponse<SopDetailResponse>.Ok(MapToDetailResponse(sop, approvalHistory, versionHistory));
        }

        public async Task<ApiResponse<SopDetailResponse>> CreateAsync(
            CreateSopRequest request, IFormFile document, Guid createdBy)
        {
            var filePath = await SaveDocumentAsync(document, createdBy);

            var sop = new SopDetails
            {
                SopTitle = request.SopTitle,
                ExpirationDate = request.ExpirationDate,
                SopDocument = filePath,
                SopDocumentVersion = 1,
                Remark = request.Remark,
                ApprovalStatus = (int)SopStatus.NotStarted,
                ApprovalLevel = 0,
                CreatedByID = createdBy
            };

            var id = await _dal.CreateAsync(sop);
            sop.ID = id;

            await _dal.AddHistoryAsync(new SopDetailsHistory
            {
                SopDetailsID = id,
                Name = sop.SopTitle,
                FileName = sop.SopDocument,
                ApprovalStatus = sop.ApprovalStatus,
                ApprovalLevel = sop.ApprovalLevel,
                ExpiryDate = sop.ExpirationDate,
                Remarks = sop.Remark,
                CreatedByID = createdBy
            });

            return ApiResponse<SopDetailResponse>.Ok(
                MapToDetailResponse(sop, new(), new()), "SOP created successfully.");
        }

        public async Task<ApiResponse<SopDetailResponse>> UpdateAsync(
            Guid id, UpdateSopRequest request, IFormFile? newDocument, Guid modifiedBy)
        {
            var sop = await _dal.GetByIdAsync(id);
            if (sop == null) return ApiResponse<SopDetailResponse>.Fail("SOP not found.");

            if (sop.ExpirationDate.HasValue && sop.ExpirationDate.Value < DateTime.UtcNow)
                return ApiResponse<SopDetailResponse>.Fail("Cannot edit an expired SOP.");

            if (!string.IsNullOrWhiteSpace(request.SopTitle)) sop.SopTitle = request.SopTitle;
            if (request.ExpirationDate.HasValue) sop.ExpirationDate = request.ExpirationDate;
            if (!string.IsNullOrWhiteSpace(request.Remark)) sop.Remark = request.Remark;

            if (newDocument != null)
            {
                sop.SopDocument = await SaveDocumentAsync(newDocument, modifiedBy);
                sop.SopDocumentVersion++;
            }

            sop.ModifiedByID = modifiedBy;
            await _dal.UpdateAsync(sop);

            await _dal.AddHistoryAsync(new SopDetailsHistory
            {
                SopDetailsID = sop.ID,
                Name = sop.SopTitle,
                FileName = sop.SopDocument,
                ApprovalStatus = sop.ApprovalStatus,
                ApprovalLevel = sop.ApprovalLevel,
                ExpiryDate = sop.ExpirationDate,
                Remarks = sop.Remark,
                CreatedByID = modifiedBy
            });

            return ApiResponse<SopDetailResponse>.Ok(
                MapToDetailResponse(sop, new(), new()), "SOP updated.");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid deletedBy)
        {
            var sop = await _dal.GetByIdAsync(id);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            await _dal.DeleteAsync(id, deletedBy);
            return ApiResponse<bool>.Ok(true, "SOP deleted.");
        }

        public async Task<ApiResponse<byte[]>> ExportCsvAsync()
        {
            var (_, sops) = await _dal.GetAllAsync(1, int.MaxValue, null, null);
            var lines = new List<string> { "SopId,SopDocumentName,DateOfExpiry,StatusOfSopSubmission" };

            foreach (var sop in sops)
                lines.Add(string.Join(",",
                    sop.ID,
                    $"\"{sop.SopTitle}\"",
                    sop.ExpirationDate?.ToString("yyyy-MM-dd") ?? "N/A",
                    ((SopStatus)sop.ApprovalStatus).ToString()));

            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines));
            return ApiResponse<byte[]>.Ok(bytes, "Export ready.");
        }

        // ── INITIATOR ──────────────────────────────────────────────────────────

        public async Task<ApiResponse<List<SopListResponse>>> GetAvailableForInitiatorAsync()
        {
            var sops = await _dal.GetActiveSopsAsync();
            var filtered = sops
                .Where(s => s.ApprovalStatus != (int)SopStatus.Completed)
                .Select(MapToListResponse)
                .ToList();

            return ApiResponse<List<SopListResponse>>.Ok(filtered);
        }

        public async Task<ApiResponse<bool>> SubmitAsync(SubmitSopRequest request, Guid submittedBy)
        {
            var sop = await _dal.GetByIdAsync(request.SopID);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            if (sop.ApprovalStatus == (int)SopStatus.Submitted || sop.ApprovalStatus == (int)SopStatus.Completed)
                return ApiResponse<bool>.Fail("SOP is already submitted or completed.");

            if (sop.ExpirationDate.HasValue && sop.ExpirationDate.Value < DateTime.UtcNow)
                return ApiResponse<bool>.Fail("Cannot submit an expired SOP.");

            sop.ApprovalStatus = (int)SopStatus.Submitted;
            sop.ModifiedByID = submittedBy;
            await _dal.UpdateAsync(sop);

            await AddApprovalRecord(sop.ID, 0, (int)ApprovalStatus.Pending, request.Comments, submittedBy, sop.SopDocumentVersion);
            await AddSopHistory(sop, submittedBy);

            return ApiResponse<bool>.Ok(true, "SOP submitted for supervisor review.");
        }

        // ── SUPERVISOR ─────────────────────────────────────────────────────────

        public async Task<ApiResponse<List<SopListResponse>>> GetPendingForSupervisorAsync()
        {
            var sops = await _dal.GetByStatusAsync((int)SopStatus.Submitted);
            return ApiResponse<List<SopListResponse>>.Ok(sops.Select(MapToListResponse).ToList());
        }

        public async Task<ApiResponse<bool>> SupervisorForwardAsync(
            Guid sopId, SupervisorActionRequest request, Guid supervisorId)
        {
            var sop = await _dal.GetByIdAsync(sopId);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            if (sop.ApprovalStatus != (int)SopStatus.Submitted)
                return ApiResponse<bool>.Fail("SOP must be in Submitted status for supervisor action.");

            sop.ApprovalStatus = (int)SopStatus.PendingApprovalLevel1;
            sop.ApprovalLevel = 1;
            sop.ModifiedByID = supervisorId;
            await _dal.UpdateAsync(sop);

            await AddApprovalRecord(sop.ID, 0, (int)ApprovalStatus.Approved, request.Comments, supervisorId, sop.SopDocumentVersion);
            await AddSopHistory(sop, supervisorId);

            return ApiResponse<bool>.Ok(true, "SOP forwarded to Level 1 Approver.");
        }

        public async Task<ApiResponse<bool>> SupervisorRequestChangesAsync(
            Guid sopId, SupervisorActionRequest request, Guid supervisorId)
        {
            var sop = await _dal.GetByIdAsync(sopId);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            if (sop.ApprovalStatus != (int)SopStatus.Submitted)
                return ApiResponse<bool>.Fail("SOP must be in Submitted status.");

            sop.ApprovalStatus = (int)SopStatus.InProgress;
            sop.ModifiedByID = supervisorId;
            await _dal.UpdateAsync(sop);

            await AddApprovalRecord(sop.ID, 0, (int)ApprovalStatus.NeedsChanges, request.Comments, supervisorId, sop.SopDocumentVersion);
            await AddSopHistory(sop, supervisorId);

            return ApiResponse<bool>.Ok(true, "SOP returned to initiator for changes.");
        }

        // ── APPROVER ───────────────────────────────────────────────────────────

        public async Task<ApiResponse<List<SopListResponse>>> GetPendingForApproverAsync(Guid approverId)
        {
            var levels = await _dal.GetApproverLevelsForEmployeeAsync(approverId);
            if (!levels.Any()) return ApiResponse<List<SopListResponse>>.Ok(new());

            var pendingStatuses = levels.Select(l => l switch
            {
                1 => (int)SopStatus.PendingApprovalLevel1,
                2 => (int)SopStatus.PendingApprovalLevel2,
                3 => (int)SopStatus.PendingApprovalLevel3,
                _ => (int)SopStatus.PendingApprovalLevel1
            }).ToList();

            var result = new List<SopDetails>();
            foreach (var status in pendingStatuses)
                result.AddRange(await _dal.GetByStatusAsync(status));

            return ApiResponse<List<SopListResponse>>.Ok(
                result.DistinctBy(s => s.ID).Select(MapToListResponse).ToList());
        }

        public async Task<ApiResponse<bool>> ProcessApprovalAsync(ApprovalActionRequest request, Guid approverId)
        {
            var sop = await _dal.GetByIdAsync(request.SopID);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            var currentLevel = sop.ApprovalLevel;

            // Validate approver is authorised for this level
            if (!await _dal.IsApproverForLevelAsync(approverId, currentLevel))
                return ApiResponse<bool>.Fail($"You are not authorised to approve at Level {currentLevel}.");

            if (request.Action == ApprovalStatus.Rejected)
            {
                sop.ApprovalStatus = (int)SopStatus.Rejected;
                sop.ModifiedByID = approverId;
                await _dal.UpdateAsync(sop);

                await AddApprovalRecord(sop.ID, currentLevel, (int)ApprovalStatus.Rejected,
                    request.Comments, approverId, sop.SopDocumentVersion);
                await AddSopHistory(sop, approverId);

                return ApiResponse<bool>.Ok(true, $"SOP rejected at Level {currentLevel}. Returned to initiator.");
            }

            if (request.Action == ApprovalStatus.Approved)
            {
                await AddApprovalRecord(sop.ID, currentLevel, (int)ApprovalStatus.Approved,
                    request.Comments, approverId, sop.SopDocumentVersion);

                if (currentLevel >= AppConstants.MaxApprovalLevels)
                {
                    sop.ApprovalStatus = (int)SopStatus.Completed;
                }
                else
                {
                    var nextLevel = currentLevel + 1;
                    sop.ApprovalLevel = nextLevel;
                    sop.ApprovalStatus = nextLevel switch
                    {
                        2 => (int)SopStatus.PendingApprovalLevel2,
                        3 => (int)SopStatus.PendingApprovalLevel3,
                        _ => (int)SopStatus.PendingApprovalLevel1
                    };
                }

                sop.ModifiedByID = approverId;
                await _dal.UpdateAsync(sop);
                await AddSopHistory(sop, approverId);

                var msg = sop.ApprovalStatus == (int)SopStatus.Completed
                    ? "SOP fully approved and marked as Completed."
                    : $"SOP approved at Level {currentLevel}. Forwarded to Level {currentLevel + 1}.";

                return ApiResponse<bool>.Ok(true, msg);
            }

            return ApiResponse<bool>.Fail("Invalid approval action.");
        }

        // ── PDF ────────────────────────────────────────────────────────────────

        public async Task<ApiResponse<byte[]>> DownloadAsync(Guid id)
        {
            var sop = await _dal.GetByIdAsync(id);
            if (sop == null) return ApiResponse<byte[]>.Fail("SOP not found.");

            if (sop.ApprovalStatus != (int)SopStatus.Completed)
                return ApiResponse<byte[]>.Fail("Only completed SOPs are available for download.");

            if (string.IsNullOrEmpty(sop.SopDocument) || !File.Exists(sop.SopDocument))
                return ApiResponse<byte[]>.Fail("Document file not found on server.");

            var bytes = await File.ReadAllBytesAsync(sop.SopDocument);
            return ApiResponse<byte[]>.Ok(bytes, "File ready.");
        }

        // ── WORKFLOW SETUP ─────────────────────────────────────────────────────

        public async Task<ApiResponse<List<WorkflowStageResponse>>> GetWorkflowStagesAsync()
        {
            var stages = await _dal.GetAllWorkflowStagesAsync();
            return ApiResponse<List<WorkflowStageResponse>>.Ok(stages.Select(MapToWorkflowResponse).ToList());
        }

        public async Task<ApiResponse<WorkflowStageResponse>> SetupWorkflowStageAsync(
            SetupWorkflowStageRequest request, Guid createdBy)
        {
            var stage = new SopDetailsWorkFlowSetUp
            {
                StageName = request.StageName,
                ApprovalLevel = request.ApprovalLevel,
                IsSupervisor = request.IsSupervisor,
                EmployeeGroupID = request.EmployeeGroupID,
                CreatedByID = createdBy
            };

            var id = await _dal.CreateWorkflowStageAsync(stage);
            stage.ID = id;

            // Re-fetch to get group name
            var saved = await _dal.GetWorkflowByLevelAsync(request.ApprovalLevel);
            return ApiResponse<WorkflowStageResponse>.Ok(
                MapToWorkflowResponse(saved ?? stage), "Workflow stage created.");
        }

        public async Task<ApiResponse<List<WorkflowStageResponse>>> BulkCreateWorkflowStagesAsync(
            List<SetupWorkflowStageRequest> requests, Guid createdBy)
        {
            var results = new List<WorkflowStageResponse>();
            foreach (var request in requests)
            {
                var stage = new SopDetailsWorkFlowSetUp
                {
                    StageName = request.StageName,
                    ApprovalLevel = request.ApprovalLevel,
                    IsSupervisor = request.IsSupervisor,
                    EmployeeGroupID = request.EmployeeGroupID,
                    CreatedByID = createdBy
                };
                var id = await _dal.CreateWorkflowStageAsync(stage);
                stage.ID = id;
                var saved = await _dal.GetWorkflowByLevelAsync(request.ApprovalLevel);
                results.Add(MapToWorkflowResponse(saved ?? stage));
            }
            return ApiResponse<List<WorkflowStageResponse>>.Ok(results,
                $"{results.Count} workflow stage(s) created.");
        }

        public async Task<ApiResponse<WorkflowStageResponse>> UpdateWorkflowStageAsync(
            Guid id, SetupWorkflowStageRequest request, Guid modifiedBy)
        {
            var all = await _dal.GetAllWorkflowStagesAsync();
            var existing = all.FirstOrDefault(w => w.ID == id);
            if (existing == null)
                return ApiResponse<WorkflowStageResponse>.Fail("Workflow stage not found.");

            existing.StageName = request.StageName;
            existing.ApprovalLevel = request.ApprovalLevel;
            existing.IsSupervisor = request.IsSupervisor;
            existing.EmployeeGroupID = request.EmployeeGroupID;
            existing.ModifiedByID = modifiedBy;

            await _dal.UpdateWorkflowStageAsync(existing);

            var saved = await _dal.GetWorkflowByLevelAsync(existing.ApprovalLevel);
            return ApiResponse<WorkflowStageResponse>.Ok(
                MapToWorkflowResponse(saved ?? existing), "Workflow stage updated.");
        }

        public async Task<ApiResponse<bool>> DeleteWorkflowStageAsync(Guid id, Guid deletedBy)
        {
            var all = await _dal.GetAllWorkflowStagesAsync();
            if (!all.Any(w => w.ID == id))
                return ApiResponse<bool>.Fail("Workflow stage not found.");

            await _dal.DeleteWorkflowStageAsync(id, deletedBy);
            return ApiResponse<bool>.Ok(true, "Workflow stage deleted.");
        }

        // ── PRIVATE HELPERS ────────────────────────────────────────────────────

        private async Task<string> SaveDocumentAsync(IFormFile file, Guid uploadedBy)
        {
            var uploadDir = Path.Combine("Uploads", "SOPs");
            Directory.CreateDirectory(uploadDir);
            var fileName = $"SOP_{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8]}.pdf";
            var fullPath = Path.Combine(uploadDir, fileName);
            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return fullPath;
        }

        private async Task AddSopHistory(SopDetails sop, Guid createdBy)
        {
            await _dal.AddHistoryAsync(new SopDetailsHistory
            {
                SopDetailsID = sop.ID,
                Name = sop.SopTitle,
                FileName = sop.SopDocument,
                ApprovalStatus = sop.ApprovalStatus,
                ApprovalLevel = sop.ApprovalLevel,
                ExpiryDate = sop.ExpirationDate,
                Remarks = sop.Remark,
                CreatedByID = createdBy
            });
        }

        private async Task AddApprovalRecord(Guid sopId, int level, int status,
            string? comments, Guid actionBy, int version)
        {
            await _dal.AddApprovalHistoryAsync(new SopDetailsApprovalHistory
            {
                SopDetailsID = sopId,
                ApprovalLevel = level,
                ApprovalStatus = status,
                Comments = comments,
                ReferenceVersion = version,
                CreatedByID = actionBy
            });
        }

        private static SopListResponse MapToListResponse(SopDetails s) => new()
        {
            ID = s.ID,
            SopTitle = s.SopTitle,
            ExpirationDate = s.ExpirationDate,
            Status = (SopStatus)s.ApprovalStatus,
            CurrentApprovalLevel = s.ApprovalLevel,
            DocumentVersion = s.SopDocumentVersion,
            Created = s.Created
        };

        private static SopDetailResponse MapToDetailResponse(
            SopDetails s,
            List<SopDetailsApprovalHistory> approvalHistory,
            List<SopDetailsHistory> versionHistory) => new()
            {
                ID = s.ID,
                SopTitle = s.SopTitle,
                SopDocument = s.SopDocument,
                ExpirationDate = s.ExpirationDate,
                Status = (SopStatus)s.ApprovalStatus,
                CurrentApprovalLevel = s.ApprovalLevel,
                DocumentVersion = s.SopDocumentVersion,
                Remark = s.Remark,
                Created = s.Created,
                ApprovalHistory = approvalHistory.Select(ah => new SopApprovalHistoryResponse
                {
                    ID = ah.ID,
                    ApprovalLevel = ah.ApprovalLevel,
                    ApprovalStatus = ah.ApprovalStatus,
                    Comments = ah.Comments,
                    ActionDate = ah.Created
                }).ToList(),
                VersionHistory = versionHistory.Select(h => new SopVersionHistoryResponse
                {
                    ID = h.ID,
                    Name = h.Name,
                    FileName = h.FileName,
                    Status = h.ApprovalStatus,
                    ApprovalLevel = h.ApprovalLevel,
                    ExpiryDate = h.ExpiryDate,
                    Remarks = h.Remarks,
                    Created = h.Created
                }).ToList()
            };

        private static WorkflowStageResponse MapToWorkflowResponse(SopDetailsWorkFlowSetUp wf) => new()
        {
            ID = wf.ID,
            StageName = wf.StageName ?? string.Empty,
            ApprovalLevel = wf.ApprovalLevel,
            IsSupervisor = wf.IsSupervisor,
            EmployeeGroupID = wf.EmployeeGroupID,
            GroupName = wf.GroupName
        };
    }
}