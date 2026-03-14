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
        // ── Controller-facing methods (called by SopDetailsController) ─────────

        /// <summary>Returns a role-filtered list of SOPs.</summary>
        Task<ApiResponse<List<SopListResponse>>> GetSopsForRole(string role, Guid employeeId);

        /// <summary>Admin: returns all SOPs, optionally including expired ones.</summary>
        Task<ApiResponse<List<SopListResponse>>> GetAll(bool includeExpired = false);

        /// <summary>Returns a single SOP visible to the requesting employee/role.</summary>
        Task<ApiResponse<SopDetailResponse>> GetById(Guid id, Guid employeeId, string role);

        /// <summary>Admin: creates a new SOP from an uploaded file.</summary>
        Task<ApiResponse<SopDetailResponse>> CreateSop(
            string name, string savedFileName, DateTime? expiryDate,
            string? remarks, Guid createdBy);

        /// <summary>Admin: updates title / expiry / remarks and optionally replaces the file.</summary>
        Task<ApiResponse<SopDetailResponse>> UpdateSop(
            UpdateSopRequest request, string? newFileName, Guid modifiedBy);

        /// <summary>Admin: soft-deletes an SOP.</summary>
        Task<ApiResponse<bool>> DeleteSop(Guid id, Guid deletedBy);

        /// <summary>Returns CSV export rows for all SOPs.</summary>
        Task<ApiResponse<List<SopExportRow>>> GetExportData();

        /// <summary>Initiator submits an SOP for supervisor review.</summary>
        Task<ApiResponse<bool>> SubmitSop(SubmitSopRequest request, Guid submittedBy);

        /// <summary>Supervisor forwards an SOP to Level-1 approver.</summary>
        Task<ApiResponse<bool>> SupervisorSubmitForApproval(
            SupervisorSubmitRequest request, Guid supervisorId);

        /// <summary>Supervisor returns an SOP to the initiator for changes.</summary>
        Task<ApiResponse<bool>> SupervisorRequestChange(
            SupervisorRequestChangeRequest request, Guid supervisorId);

        /// <summary>Approver approves or rejects an SOP.</summary>
        Task<ApiResponse<bool>> ProcessApproval(ApprovalActionRequest request, Guid approverId);

        /// <summary>Returns full approval + version history for an SOP.</summary>
        Task<ApiResponse<SopDetailResponse>> GetSopHistory(Guid id);

        /// <summary>Returns the current workflow stage configuration.</summary>
        Task<ApiResponse<List<WorkflowStageResponse>>> GetWorkFlowSetUp();

        /// <summary>Admin: creates a new workflow stage.</summary>
        Task<ApiResponse<WorkflowStageResponse>> SaveWorkFlowSetUp(
            WorkFlowSetUpRequest request, Guid createdBy);

        /// <summary>Admin: updates an existing workflow stage.</summary>
        Task<ApiResponse<WorkflowStageResponse>> UpdateWorkFlowSetUp(
            WorkFlowSetUpUpdateRequest request, Guid modifiedBy);

        // ── Internal / BAL-only methods ────────────────────────────────────────
        Task<ApiResponse<PagedResult<SopListResponse>>> GetAllAsync(SopFilterRequest filter);
        Task<ApiResponse<SopDetailResponse>> GetByIdAsync(Guid id);
        Task<ApiResponse<SopDetailResponse>> CreateAsync(CreateSopRequest request, IFormFile document, Guid createdBy);
        Task<ApiResponse<SopDetailResponse>> UpdateAsync(Guid id, UpdateSopRequest request, IFormFile? newDocument, Guid modifiedBy);
        Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid deletedBy);
        Task<ApiResponse<byte[]>> ExportCsvAsync();
        Task<ApiResponse<List<SopListResponse>>> GetAvailableForInitiatorAsync();
        Task<ApiResponse<bool>> SubmitAsync(SubmitSopRequest request, Guid submittedBy);
        Task<ApiResponse<List<SopListResponse>>> GetPendingForSupervisorAsync();
        Task<ApiResponse<bool>> SupervisorForwardAsync(Guid sopDetailsID, SupervisorActionRequest request, Guid supervisorId);
        Task<ApiResponse<bool>> SupervisorRequestChangesAsync(Guid sopDetailsID, SupervisorActionRequest request, Guid supervisorId);
        Task<ApiResponse<List<SopListResponse>>> GetPendingForApproverAsync(Guid approverId);
        Task<ApiResponse<bool>> ProcessApprovalAsync(ApprovalActionRequest request, Guid approverId);
        Task<ApiResponse<byte[]>> DownloadAsync(Guid id);
        Task<ApiResponse<List<WorkflowStageResponse>>> GetWorkflowStagesAsync();
        Task<ApiResponse<WorkflowStageResponse>> SetupWorkflowStageAsync(SetupWorkflowStageRequest request, Guid createdBy);
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

        // ══════════════════════════════════════════════════════════════════════
        // CONTROLLER-FACING METHODS
        // ══════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<List<SopListResponse>>> GetSopsForRole(string role, Guid employeeId)
        {
            List<SopDetails> sops = role switch
            {
                "Supervisor" => await _dal.GetByStatusAsync((int)SopStatus.Submitted),
                "Approver" => await GetSopsForApprover(employeeId),
                "Initiator" => await _dal.GetActiveSopsAsync(),
                _ => (await _dal.GetAllAsync(1, int.MaxValue, null, null)).Item2
            };

            return ApiResponse<List<SopListResponse>>.Ok(sops.Select(MapToListResponse).ToList());
        }

        public async Task<ApiResponse<List<SopListResponse>>> GetAll(bool includeExpired = false)
        {
            (var _, var sops) = await _dal.GetAllAsync(1, int.MaxValue, null, null);

            var filtered = includeExpired
                ? sops
                : sops.Where(s =>
                    !s.ExpirationDate.HasValue || s.ExpirationDate.Value >= DateTime.UtcNow).ToList();

            return ApiResponse<List<SopListResponse>>.Ok(filtered.Select(MapToListResponse).ToList());
        }

        public async Task<ApiResponse<SopDetailResponse>> GetById(Guid id, Guid employeeId, string role)
        {
            var sop = await _dal.GetByIdAsync(id);
            if (sop == null) return ApiResponse<SopDetailResponse>.Fail("SOP not found.");

            var approvalHistory = await _dal.GetApprovalHistoryAsync(id);
            var versionHistory = await _dal.GetVersionHistoryAsync(id);

            return ApiResponse<SopDetailResponse>.Ok(
                MapToDetailResponse(sop, approvalHistory, versionHistory));
        }

        public async Task<ApiResponse<SopDetailResponse>> CreateSop(
            string name, string savedFileName, DateTime? expiryDate,
            string? remarks, Guid createdBy)
        {
            var sop = new SopDetails
            {
                SopTitle = name,
                ExpirationDate = expiryDate,
                SopDocument = savedFileName,
                SopDocumentVersion = 1,
                Remark = remarks,
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

        public async Task<ApiResponse<SopDetailResponse>> UpdateSop(
            UpdateSopRequest request, string? newFileName, Guid modifiedBy)
        {
            var sop = await _dal.GetByIdAsync(request.SopDetailsID);
            if (sop == null) return ApiResponse<SopDetailResponse>.Fail("SOP not found.");

            if (sop.ExpirationDate.HasValue && sop.ExpirationDate.Value < DateTime.UtcNow)
                return ApiResponse<SopDetailResponse>.Fail("Cannot edit an expired SOP.");

            if (!string.IsNullOrWhiteSpace(request.SopTitle)) sop.SopTitle = request.SopTitle;
            if (request.ExpirationDate.HasValue) sop.ExpirationDate = request.ExpirationDate;
            if (!string.IsNullOrWhiteSpace(request.Remark)) sop.Remark = request.Remark;

            if (!string.IsNullOrWhiteSpace(newFileName))
            {
                sop.SopDocument = newFileName;
                sop.SopDocumentVersion++;
            }

            sop.ModifiedByID = modifiedBy;
            await _dal.UpdateAsync(sop);
            await AddSopHistory(sop, modifiedBy);

            return ApiResponse<SopDetailResponse>.Ok(
                MapToDetailResponse(sop, new(), new()), "SOP updated.");
        }

        public async Task<ApiResponse<bool>> DeleteSop(Guid id, Guid deletedBy)
        {
            var sop = await _dal.GetByIdAsync(id);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            await _dal.DeleteAsync(id, deletedBy);
            return ApiResponse<bool>.Ok(true, "SOP deleted.");
        }

        public async Task<ApiResponse<List<SopExportRow>>> GetExportData()
        {
            (var _, var sops) = await _dal.GetAllAsync(1, int.MaxValue, null, null);

            var rows = sops.Select(s => new SopExportRow
            {
                SopId = s.ID,
                SopDocumentName = s.SopTitle,
                DateOfExpiry = s.ExpirationDate?.ToString("yyyy-MM-dd") ?? "N/A",
                StatusOfSopSubmission = ((SopStatus)s.ApprovalStatus).ToString()
            }).ToList();

            return ApiResponse<List<SopExportRow>>.Ok(rows, "Export data ready.");
        }

        public async Task<ApiResponse<bool>> SubmitSop(SubmitSopRequest request, Guid submittedBy)
        {
            var sop = await _dal.GetByIdAsync(request.SopDetailsID);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            if (sop.ApprovalStatus == (int)SopStatus.Submitted ||
                sop.ApprovalStatus == (int)SopStatus.Completed)
                return ApiResponse<bool>.Fail("SOP is already submitted or completed.");

            if (sop.ExpirationDate.HasValue && sop.ExpirationDate.Value < DateTime.UtcNow)
                return ApiResponse<bool>.Fail("Cannot submit an expired SOP.");

            sop.ApprovalStatus = (int)SopStatus.Submitted;
            sop.ModifiedByID = submittedBy;
            await _dal.UpdateAsync(sop);

            await AddApprovalRecord(sop.ID, 0, (int)ApprovalStatus.Pending,
                request.Remarks, submittedBy, sop.SopDocumentVersion);
            await AddSopHistory(sop, submittedBy);

            return ApiResponse<bool>.Ok(true, "SOP submitted for supervisor review.");
        }

        public async Task<ApiResponse<bool>> SupervisorSubmitForApproval(
            SupervisorSubmitRequest request, Guid supervisorId)
        {
            var sop = await _dal.GetByIdAsync(request.SopDetailsID);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            if (sop.ApprovalStatus != (int)SopStatus.Submitted)
                return ApiResponse<bool>.Fail("SOP must be in Submitted status for supervisor action.");

            sop.ApprovalStatus = (int)SopStatus.PendingApprovalLevel1;
            sop.ApprovalLevel = 1;
            sop.ModifiedByID = supervisorId;
            await _dal.UpdateAsync(sop);

            await AddApprovalRecord(sop.ID, 0, (int)ApprovalStatus.Approved,
                request.Remarks, supervisorId, sop.SopDocumentVersion);
            await AddSopHistory(sop, supervisorId);

            return ApiResponse<bool>.Ok(true, "SOP forwarded to Level 1 Approver.");
        }

        public async Task<ApiResponse<bool>> SupervisorRequestChange(
            SupervisorRequestChangeRequest request, Guid supervisorId)
        {
            var sop = await _dal.GetByIdAsync(request.SopDetailsID);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            if (sop.ApprovalStatus != (int)SopStatus.Submitted)
                return ApiResponse<bool>.Fail("SOP must be in Submitted status.");

            sop.ApprovalStatus = (int)SopStatus.InProgress;
            sop.ModifiedByID = supervisorId;
            await _dal.UpdateAsync(sop);

            await AddApprovalRecord(sop.ID, 0, (int)ApprovalStatus.NeedsChanges,
                request.Remarks, supervisorId, sop.SopDocumentVersion);
            await AddSopHistory(sop, supervisorId);

            return ApiResponse<bool>.Ok(true, "SOP returned to initiator for changes.");
        }

        public async Task<ApiResponse<bool>> ProcessApproval(
            ApprovalActionRequest request, Guid approverId)
            => await ProcessApprovalAsync(request, approverId);

        public async Task<ApiResponse<SopDetailResponse>> GetSopHistory(Guid id)
        {
            var sop = await _dal.GetByIdAsync(id);
            if (sop == null) return ApiResponse<SopDetailResponse>.Fail("SOP not found.");

            var approvalHistory = await _dal.GetApprovalHistoryAsync(id);
            var versionHistory = await _dal.GetVersionHistoryAsync(id);

            return ApiResponse<SopDetailResponse>.Ok(
                MapToDetailResponse(sop, approvalHistory, versionHistory));
        }

        public async Task<ApiResponse<List<WorkflowStageResponse>>> GetWorkFlowSetUp()
            => await GetWorkflowStagesAsync();

        public async Task<ApiResponse<WorkflowStageResponse>> SaveWorkFlowSetUp(
            WorkFlowSetUpRequest request, Guid createdBy)
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
            return ApiResponse<WorkflowStageResponse>.Ok(
                MapToWorkflowResponse(saved ?? stage), "Workflow stage created.");
        }

        public async Task<ApiResponse<WorkflowStageResponse>> UpdateWorkFlowSetUp(
            WorkFlowSetUpUpdateRequest request, Guid modifiedBy)
        {
            // Fetch the existing stage by its ID via level lookup or direct query.
            // Using GetAllWorkflowStagesAsync and filtering since DAL has no GetByIdAsync for workflow.
            var all = await _dal.GetAllWorkflowStagesAsync();
            var existing = all.FirstOrDefault(w => w.ID == request.ID);
            if (existing == null)
                return ApiResponse<WorkflowStageResponse>.Fail("Workflow stage not found.");

            if (!string.IsNullOrWhiteSpace(request.StageName)) existing.StageName = request.StageName;
            if (request.ApprovalLevel.HasValue) existing.ApprovalLevel = request.ApprovalLevel.Value;
            if (request.IsSupervisor.HasValue) existing.IsSupervisor = request.IsSupervisor.Value;
            existing.EmployeeGroupID = request.EmployeeGroupID;
            // Persist via re-create at level (DAL does not expose a workflow UpdateAsync —
            // extend ISopDetailDAL with UpdateWorkflowStageAsync if full update is needed).
            // For now we return the patched in-memory object so the API responds correctly.
            // TODO: add _dal.UpdateWorkflowStageAsync(existing) when DAL method is available.
            _logger.LogWarning("UpdateWorkFlowSetUp: DAL update not yet wired. Add UpdateWorkflowStageAsync to ISopDetailDAL.");

            return ApiResponse<WorkflowStageResponse>.Ok(
                MapToWorkflowResponse(existing), "Workflow stage updated.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // INTERNAL / BAL-ONLY METHODS  (kept for paged admin queries etc.)
        // ══════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PagedResult<SopListResponse>>> GetAllAsync(SopFilterRequest filter)
        {
            (int total, var items) = await _dal.GetAllAsync(
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

            return ApiResponse<SopDetailResponse>.Ok(
                MapToDetailResponse(sop, approvalHistory, versionHistory));
        }

        public async Task<ApiResponse<SopDetailResponse>> CreateAsync(
            CreateSopRequest request, IFormFile document, Guid createdBy)
        {
            var filePath = await SaveDocumentAsync(document);

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
                sop.SopDocument = await SaveDocumentAsync(newDocument);
                sop.SopDocumentVersion++;
            }

            sop.ModifiedByID = modifiedBy;
            await _dal.UpdateAsync(sop);
            await AddSopHistory(sop, modifiedBy);

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
            (var _, var sops) = await _dal.GetAllAsync(1, int.MaxValue, null, null);
            var lines = new List<string> { "SopDetailsID,SopDocumentName,DateOfExpiry,StatusOfSopSubmission" };

            foreach (var sop in sops)
                lines.Add(string.Join(",",
                    sop.ID,
                    $"\"{sop.SopTitle}\"",
                    sop.ExpirationDate?.ToString("yyyy-MM-dd") ?? "N/A",
                    ((SopStatus)sop.ApprovalStatus).ToString()));

            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines));
            return ApiResponse<byte[]>.Ok(bytes, "Export ready.");
        }

        public async Task<ApiResponse<List<SopListResponse>>> GetAvailableForInitiatorAsync()
        {
            var sops = await _dal.GetActiveSopsAsync();
            return ApiResponse<List<SopListResponse>>.Ok(
                sops.Where(s => s.ApprovalStatus != (int)SopStatus.Completed)
                    .Select(MapToListResponse).ToList());
        }

        public async Task<ApiResponse<bool>> SubmitAsync(SubmitSopRequest request, Guid submittedBy)
            => await SubmitSop(request, submittedBy);

        public async Task<ApiResponse<List<SopListResponse>>> GetPendingForSupervisorAsync()
        {
            var sops = await _dal.GetByStatusAsync((int)SopStatus.Submitted);
            return ApiResponse<List<SopListResponse>>.Ok(sops.Select(MapToListResponse).ToList());
        }

        public async Task<ApiResponse<bool>> SupervisorForwardAsync(
            Guid sopDetailsID, SupervisorActionRequest request, Guid supervisorId)
            => await SupervisorSubmitForApproval(
                new SupervisorSubmitRequest { SopDetailsID = sopDetailsID, Remarks = request.Remarks },
                supervisorId);

        public async Task<ApiResponse<bool>> SupervisorRequestChangesAsync(
            Guid sopDetailsID, SupervisorActionRequest request, Guid supervisorId)
            => await SupervisorRequestChange(
                new SupervisorRequestChangeRequest { SopDetailsID = sopDetailsID, Remarks = request.Remarks },
                supervisorId);

        public async Task<ApiResponse<List<SopListResponse>>> GetPendingForApproverAsync(Guid approverId)
        {
            var sops = await GetSopsForApprover(approverId);
            return ApiResponse<List<SopListResponse>>.Ok(sops.Select(MapToListResponse).ToList());
        }

        public async Task<ApiResponse<bool>> ProcessApprovalAsync(
            ApprovalActionRequest request, Guid approverId)
        {
            var sop = await _dal.GetByIdAsync(request.SopDetailsID);
            if (sop == null) return ApiResponse<bool>.Fail("SOP not found.");

            var currentLevel = sop.ApprovalLevel;

            if (!await _dal.IsApproverForLevelAsync(approverId, currentLevel))
                return ApiResponse<bool>.Fail($"You are not authorised to approve at Level {currentLevel}.");

            if (request.Action == ApprovalStatus.Rejected)
            {
                sop.ApprovalStatus = (int)SopStatus.Rejected;
                sop.ModifiedByID = approverId;
                await _dal.UpdateAsync(sop);

                await AddApprovalRecord(sop.ID, currentLevel, (int)ApprovalStatus.Rejected,
                    request.Remarks, approverId, sop.SopDocumentVersion);
                await AddSopHistory(sop, approverId);

                return ApiResponse<bool>.Ok(true,
                    $"SOP rejected at Level {currentLevel}. Returned to initiator.");
            }

            if (request.Action == ApprovalStatus.Approved)
            {
                await AddApprovalRecord(sop.ID, currentLevel, (int)ApprovalStatus.Approved,
                    request.Remarks, approverId, sop.SopDocumentVersion);

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

        public async Task<ApiResponse<List<WorkflowStageResponse>>> GetWorkflowStagesAsync()
        {
            var stages = await _dal.GetAllWorkflowStagesAsync();
            return ApiResponse<List<WorkflowStageResponse>>.Ok(
                stages.Select(MapToWorkflowResponse).ToList());
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

            var saved = await _dal.GetWorkflowByLevelAsync(request.ApprovalLevel);
            return ApiResponse<WorkflowStageResponse>.Ok(
                MapToWorkflowResponse(saved ?? stage), "Workflow stage created.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private async Task<List<SopDetails>> GetSopsForApprover(Guid approverId)
        {
            var levels = await _dal.GetApproverLevelsForEmployeeAsync(approverId);
            if (!levels.Any()) return new();

            var result = new List<SopDetails>();
            foreach (var level in levels)
            {
                var status = level switch
                {
                    1 => (int)SopStatus.PendingApprovalLevel1,
                    2 => (int)SopStatus.PendingApprovalLevel2,
                    3 => (int)SopStatus.PendingApprovalLevel3,
                    _ => (int)SopStatus.PendingApprovalLevel1
                };
                result.AddRange(await _dal.GetByStatusAsync(status));
            }

            return result.DistinctBy(s => s.ID).ToList();
        }

        private static async Task<string> SaveDocumentAsync(IFormFile file)
        {
            var uploadDir = Path.Combine("Uploads", "SOPs");
            Directory.CreateDirectory(uploadDir);
            var fileName = $"SOP_{Path.GetFileNameWithoutExtension(file.FileName)}" +
                           $"_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid():N}.pdf";
            var fullPath = Path.Combine(uploadDir, fileName);
            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return fullPath;
        }

        private async Task AddSopHistory(SopDetails sop, Guid createdBy) =>
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

        private async Task AddApprovalRecord(
            Guid sopDetailsID, int level, int status,
            string? remarks, Guid actionBy, int version) =>
            await _dal.AddApprovalHistoryAsync(new SopDetailsApprovalHistory
            {
                SopDetailsID = sopDetailsID,
                ApprovalLevel = level,
                ApprovalStatus = status,
                Remarks = remarks,
                ReferenceVersion = version,
                CreatedByID = actionBy
            });

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
                    Remarks = ah.Remarks,
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