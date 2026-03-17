using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.DataModel.SOP;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IWorkFlowSetUpBAL
    {
        Task<List<WorkFlowSetUpResponse>> GetAll();
        Task<WorkFlowSetUpResponse?> GetById(Guid id);
        Task<Guid> Create(CreateWorkFlowStageRequest request, Guid userId);
        Task<List<Guid>> BulkCreate(List<CreateWorkFlowStageRequest> requests, Guid userId);
        Task<bool> Update(Guid id, UpdateWorkFlowStageRequest request, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class WorkFlowSetUpBAL : IWorkFlowSetUpBAL
    {
        private readonly IWorkFlowSetUpDAL _dal;
        private readonly IEmployeeGroupDAL _groupDal;   // to resolve GroupName
        private readonly ILogger<WorkFlowSetUpBAL> _logger;

        public WorkFlowSetUpBAL(
            IWorkFlowSetUpDAL dal,
            IEmployeeGroupDAL groupDal,
            ILogger<WorkFlowSetUpBAL> logger)
        {
            _dal = dal;
            _groupDal = groupDal;
            _logger = logger;
        }

        // ── GET ALL ──────────────────────────────────────────────────────────
        public async Task<List<WorkFlowSetUpResponse>> GetAll()
        {
            var stages = await _dal.GetAll();

            // Batch-load all group names to avoid N+1
            var groups = await _groupDal.GetAll();
            var groupMap = groups.ToDictionary(g => g.ID, g => g.Name);

            return stages.Select(s => ToResponse(s, groupMap)).ToList();
        }

        // ── GET BY ID ────────────────────────────────────────────────────────
        public async Task<WorkFlowSetUpResponse?> GetById(Guid id)
        {
            var stage = await _dal.GetById(id);
            if (stage == null) return null;

            var groups = await _groupDal.GetAll();
            var groupMap = groups.ToDictionary(g => g.ID, g => g.Name);
            return ToResponse(stage, groupMap);
        }

        // ── CREATE SINGLE ────────────────────────────────────────────────────
        public async Task<Guid> Create(CreateWorkFlowStageRequest request, Guid userId)
        {
            Validate(request.StageName, request.ApprovalLevel, request.IsSupervisor, request.EmployeeGroupID);

            var dm = new WorkFlowSetUp
            {
                ID = Guid.NewGuid(),
                StageName = request.StageName!.Trim(),
                ApprovalLevel = request.ApprovalLevel,
                IsSupervisor = request.IsSupervisor,
                EmployeeGroupID = request.EmployeeGroupID,
                CreatedByID = userId,
            };

            _logger.LogInformation("BAL: Creating WorkFlowStage [{Name}] L{Level}", dm.StageName, dm.ApprovalLevel);
            return await _dal.Create(dm);
        }

        // ── BULK CREATE ──────────────────────────────────────────────────────
        // Creates multiple stages sharing the same StageName (one POST saves all rows).
        // Each row can have different ApprovalLevel/IsSupervisor/EmployeeGroupID.
        public async Task<List<Guid>> BulkCreate(List<CreateWorkFlowStageRequest> requests, Guid userId)
        {
            if (requests == null || requests.Count == 0)
                throw new Exception("At least one stage is required.");

            var ids = new List<Guid>();
            foreach (var req in requests)
            {
                var id = await Create(req, userId);
                ids.Add(id);
            }
            return ids;
        }

        // ── UPDATE ───────────────────────────────────────────────────────────
        public async Task<bool> Update(Guid id, UpdateWorkFlowStageRequest request, Guid userId)
        {
            Validate(request.StageName, request.ApprovalLevel, request.IsSupervisor, request.EmployeeGroupID);

            var existing = await _dal.GetById(id);
            if (existing == null) throw new Exception("Workflow stage not found.");

            existing.StageName = request.StageName!.Trim();
            existing.ApprovalLevel = request.ApprovalLevel;
            existing.IsSupervisor = request.IsSupervisor;
            existing.EmployeeGroupID = request.EmployeeGroupID;
            existing.ModifiedByID = userId;

            _logger.LogInformation("BAL: Updating WorkFlowStage {Id}", id);
            return await _dal.Update(existing);
        }

        // ── DELETE ───────────────────────────────────────────────────────────
        public async Task<bool> Delete(Guid id, Guid userId)
        {
            var existing = await _dal.GetById(id);
            if (existing == null) throw new Exception("Workflow stage not found.");

            _logger.LogInformation("BAL: Deleting WorkFlowStage {Id}", id);
            return await _dal.Delete(id, userId);
        }


        // ── PRIVATE: Shared validation ────────────────────────────────────────
        private static void Validate(string? stageName, int level, bool isSupervisor, Guid? groupId)
        {
            if (string.IsNullOrWhiteSpace(stageName))
                throw new Exception("Stage Name is required.");

            if (level < 0 || level > 5)
                throw new Exception("Approval Level must be between 0 and 5.");

            // Supervisor stage (level 2) doesn't need a group — authorised via ManagerID
            if (!isSupervisor && groupId == null)
                throw new Exception("Employee Group is required for non-supervisor stages.");
        }

        // ── PRIVATE: DataModel → WebModel ─────────────────────────────────────
        private static WorkFlowSetUpResponse ToResponse(
            WorkFlowSetUp s,
            Dictionary<Guid, string> groupMap) => new()
            {
                ID = s.ID,
                StageName = s.StageName,
                ApprovalLevel = s.ApprovalLevel,
                IsSupervisor = s.IsSupervisor,
                EmployeeGroupID = s.EmployeeGroupID

            };
    }
}
