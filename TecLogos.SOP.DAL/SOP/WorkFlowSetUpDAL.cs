// Place in: TecLogos.SOP.DAL/SOP/WorkFlowSetUpDAL.cs

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IWorkFlowSetUpDAL
    {
        Task<List<WorkFlowSetUp>> GetAll();
        Task<WorkFlowSetUp?> GetById(Guid id);
        Task<Guid> Create(WorkFlowSetUp stage);
        Task<bool> Update(WorkFlowSetUp stage);
        Task<bool> Delete(Guid id, Guid deletedById);
    }

    public class WorkFlowSetUpDAL : IWorkFlowSetUpDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<WorkFlowSetUpDAL> _logger;

        public WorkFlowSetUpDAL(IConfiguration configuration, ILogger<WorkFlowSetUpDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        private SqlConnection CreateConnection() => new(_connectionString);

        // ── GET ALL ──────────────────────────────────────────────────────────
        // Joins EmployeeGroup to return GroupName alongside each stage.
        // Supervisor stage has NULL EmployeeGroupID — LEFT JOIN handles this.
        public async Task<List<WorkFlowSetUp>> GetAll()
        {
            // Note: We return the raw DataModel here; BAL maps GroupName separately.
            const string sql = @"
                SELECT
                    WF.ID,
                    WF.StageName,
                    WF.ApprovalLevel,
                    WF.IsSupervisor,
                    WF.EmployeeGroupID,
                    WF.Version,
                    WF.IsActive,
                    WF.IsDeleted,
                    WF.Created,
                    WF.CreatedByID,
                    WF.Modified,
                    WF.ModifiedByID
                FROM  [SopDetailsWorkFlowSetUp] WF
                WHERE WF.IsDeleted = 0
                ORDER BY WF.ApprovalLevel ASC;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            var list = new List<WorkFlowSetUp>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(Map(reader));

            return list;
        }

        // ── GET BY ID ────────────────────────────────────────────────────────
        public async Task<WorkFlowSetUp?> GetById(Guid id)
        {
            const string sql = @"
                SELECT
                    ID, StageName, ApprovalLevel, IsSupervisor, EmployeeGroupID,
                    Version, IsActive, IsDeleted, Created, CreatedByID, Modified, ModifiedByID
                FROM  [SopDetailsWorkFlowSetUp]
                WHERE ID        = @ID
                  AND IsDeleted = 0;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = id;

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? Map(reader) : null;
        }

        // ── CREATE ───────────────────────────────────────────────────────────
        // Inserts one stage. Caller validates uniqueness of ApprovalLevel.
        public async Task<Guid> Create(WorkFlowSetUp stage)
        {
            const string sql = @"
                INSERT INTO [SopDetailsWorkFlowSetUp]
                    (ID, StageName, ApprovalLevel, IsSupervisor, EmployeeGroupID,
                     Version, IsActive, IsDeleted, Created, CreatedByID)
                VALUES
                    (@ID, @StageName, @ApprovalLevel, @IsSupervisor, @EmployeeGroupID,
                     1, 1, 0, GETDATE(), @CreatedByID);";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = stage.ID;
            cmd.Parameters.Add("@StageName", SqlDbType.NVarChar).Value = (object?)stage.StageName ?? DBNull.Value;
            cmd.Parameters.Add("@ApprovalLevel", SqlDbType.Int).Value = stage.ApprovalLevel;
            cmd.Parameters.Add("@IsSupervisor", SqlDbType.Bit).Value = stage.IsSupervisor;
            cmd.Parameters.Add("@EmployeeGroupID", SqlDbType.UniqueIdentifier).Value = (object?)stage.EmployeeGroupID ?? DBNull.Value;
            cmd.Parameters.Add("@CreatedByID", SqlDbType.UniqueIdentifier).Value = stage.CreatedByID;

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("WorkFlowStage created. ID={Id} Level={Level}", stage.ID, stage.ApprovalLevel);
            return stage.ID;
        }

        // ── UPDATE ───────────────────────────────────────────────────────────
        public async Task<bool> Update(WorkFlowSetUp stage)
        {
            const string sql = @"
                UPDATE [SopDetailsWorkFlowSetUp]
                SET
                    StageName       = @StageName,
                    ApprovalLevel   = @ApprovalLevel,
                    IsSupervisor    = @IsSupervisor,
                    EmployeeGroupID = @EmployeeGroupID,
                    Modified        = GETDATE(),
                    ModifiedByID    = @ModifiedByID
                WHERE ID        = @ID
                  AND IsDeleted = 0;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = stage.ID;
            cmd.Parameters.Add("@StageName", SqlDbType.NVarChar).Value = (object?)stage.StageName ?? DBNull.Value;
            cmd.Parameters.Add("@ApprovalLevel", SqlDbType.Int).Value = stage.ApprovalLevel;
            cmd.Parameters.Add("@IsSupervisor", SqlDbType.Bit).Value = stage.IsSupervisor;
            cmd.Parameters.Add("@EmployeeGroupID", SqlDbType.UniqueIdentifier).Value = (object?)stage.EmployeeGroupID ?? DBNull.Value;
            cmd.Parameters.Add("@ModifiedByID", SqlDbType.UniqueIdentifier).Value = stage.ModifiedByID!;

            var rows = await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("WorkFlowStage updated. ID={Id}", stage.ID);
            return rows > 0;
        }

        // ── SOFT DELETE ──────────────────────────────────────────────────────
        // Soft-delete only — never hard-delete workflow stages.
        // Existing SOP approval history references this table.
        public async Task<bool> Delete(Guid id, Guid deletedById)
        {
            const string sql = @"
                UPDATE [SopDetailsWorkFlowSetUp]
                SET
                    IsDeleted   = 1,
                    IsActive    = 0,
                    Deleted     = GETDATE(),
                    DeletedByID = @DeletedByID
                WHERE ID        = @ID
                  AND IsDeleted = 0;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = id;
            cmd.Parameters.Add("@DeletedByID", SqlDbType.UniqueIdentifier).Value = deletedById;

            var rows = await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("WorkFlowStage soft-deleted. ID={Id}", id);
            return rows > 0;
        }

        // ── PRIVATE: Map reader row to DataModel ─────────────────────────────
        private static WorkFlowSetUp Map(SqlDataReader r) => new()
        {
            ID = r.GetGuid(r.GetOrdinal("ID")),
            StageName = r.IsDBNull(r.GetOrdinal("StageName")) ? null : r.GetString(r.GetOrdinal("StageName")),
            ApprovalLevel = r.GetInt32(r.GetOrdinal("ApprovalLevel")),
            IsSupervisor = r.GetBoolean(r.GetOrdinal("IsSupervisor")),
            EmployeeGroupID = r.IsDBNull(r.GetOrdinal("EmployeeGroupID")) ? null : r.GetGuid(r.GetOrdinal("EmployeeGroupID")),
            Version = r.GetInt32(r.GetOrdinal("Version")),
            IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
            IsDeleted = r.GetBoolean(r.GetOrdinal("IsDeleted")),
            Created = r.GetDateTime(r.GetOrdinal("Created")),
            CreatedByID = r.GetGuid(r.GetOrdinal("CreatedByID")),
            Modified = r.IsDBNull(r.GetOrdinal("Modified")) ? null : r.GetDateTime(r.GetOrdinal("Modified")),
            ModifiedByID = r.IsDBNull(r.GetOrdinal("ModifiedByID")) ? null : r.GetGuid(r.GetOrdinal("ModifiedByID")),
        };
    }
}
