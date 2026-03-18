using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.DataModel.SOP;
using TecLogos.SOP.EnumsAndConstants;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface ISopDetailBAL
    {
        Task<SopListResponse> GetAllSops(int? approvalStatus, int? year);
        Task<SopDetailResponse?> GetSopById(Guid sopId);
        Task<Guid> CreateSop(CreateSopRequest request, Guid userId, Guid sopId, string? documentPath);
        Task<bool> UpdateSop(Guid sopId, UpdateSopRequest request, Guid userId);
        Task<bool> DeleteSop(Guid sopId, Guid userId);
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
        public async Task<SopListResponse> GetAllSops(int? approvalStatus, int? year)
        {
            var (total, data) = await _dal.GetAllSops(approvalStatus, year);

            return new SopListResponse
            {
                TotalCount = total,
                Items = data.Select(SopDetailToResponse).ToList()
            };
        }
        public async Task<SopDetailResponse?> GetSopById(Guid sopId)
        {
            if (sopId == Guid.Empty)
                throw new Exception("SOP ID is required.");

            var (sop, version) = await _dal.GetSopById(sopId);

            if (sop == null)
                return null;

            return new SopDetailResponse
            {
                ID = sop.ID,
                SopTitle = sop.SopTitle,
                ExpirationDate = sop.ExpirationDate,
                SopDocument = sop.SopDocument,
                SopDocumentVersion = version,
                Remark = sop.Remark,
                ApprovalLevel = sop.ApprovalLevel,
                NextApprovalLevel = sop.NextApprovalLevel,
                ApprovalStatus = sop.ApprovalStatus
            };
        }

        public async Task<Guid> CreateSop(CreateSopRequest request, Guid userId, Guid sopId, string? documentPath)
        {
            if (string.IsNullOrWhiteSpace(request.SopTitle))
                throw new Exception("SOP Title is required.");

            if (request.ExpirationDate.HasValue && request.ExpirationDate.Value <= DateTime.UtcNow)
                throw new Exception("Expiration date must be future.");

            var dm = new DataModel.SOP.SopDetail
            {
                ID = sopId,
                SopTitle = request.SopTitle.Trim(),
                ExpirationDate = request.ExpirationDate,
                SopDocument = documentPath,
                Remark = request.Remark,
                ApprovalLevel = 0,
                ApprovalStatus = SopApprovalStatus.Pending,
                CreatedByID = userId,
                Created = DateTime.UtcNow
            };

            _logger.LogInformation("BAL: Creating SOP [{Title}] by {UserId}", dm.SopTitle, userId);

            return await _dal.CreateSop(dm);
        }
        public async Task<bool> UpdateSop(Guid sopId, UpdateSopRequest request, Guid userId)
        {
            if (sopId == Guid.Empty)
                throw new Exception("SOP ID is required.");

            if (string.IsNullOrWhiteSpace(request.SopTitle))
                throw new Exception("SOP Title is required.");

            if (request.ExpirationDate.HasValue && request.ExpirationDate.Value <= DateTime.UtcNow)
                throw new Exception("Expiration date must be future.");

            var (existingSop, currentVersion) = await _dal.GetSopById(sopId);

            if (existingSop == null)
                throw new Exception("SOP not found");

            bool isFileUploaded = request.DocumentFile != null && request.DocumentFile.Length > 0;

            string? filePath = null;

            if (isFileUploaded)
            {
                int newVersion = currentVersion + 1;

                string versionFolder = $"V{newVersion}";

                string basePath = Path.Combine(
                    "Uploads",
                    "Sop-Detail",
                    sopId.ToString(),
                    "SopDocument",
                    versionFolder
                );

                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                string fileName = Path.GetFileName(request.DocumentFile.FileName);
                string fullPath = Path.Combine(basePath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await request.DocumentFile.CopyToAsync(stream);
                }

                filePath = Path.Combine(
                    "Uploads",
                    "Sop-Detail",
                    sopId.ToString(),
                    "SopDocument",
                    versionFolder,
                    fileName
                );
            }

            return await _dal.UpdateSop(
                sopId,
                request.ExpirationDate,
                request.Remark,
                userId,
                isFileUploaded,
                filePath
            );
        }
        public async Task<bool> DeleteSop(Guid sopId, Guid userId)
        {
            if (sopId == Guid.Empty) throw new Exception("SOP ID is required.");
            if (userId == Guid.Empty) throw new Exception("User ID is required.");

            return await _dal.Delete(sopId, userId);
        }

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
               NextApprovalLevel = d.NextApprovalLevel,
               ApprovalStatus = d.ApprovalStatus

           };


    }
}