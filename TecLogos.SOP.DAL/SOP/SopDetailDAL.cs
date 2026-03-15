using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface ISopDetailDAL
    {
        Task<(int totalCount, List<SopDetails>)> GetAllAsync(int pageNumber, int pageSize, string? search, int? status);
        Task<SopDetails?> GetByIdAsync(Guid id);
        Task<SopDetails?> GetWithHistoryAsync(Guid id);
        Task<List<SopDetailsHistory>> GetVersionHistoryAsync(Guid sopId);
        Task<List<SopDetailsApprovalHistory>> GetApprovalHistoryAsync(Guid sopId);
        Task<List<SopDetails>> GetActiveSopsAsync();
        Task<List<SopDetails>> GetByStatusAsync(int status);
        Task<List<SopDetails>> GetByApprovalLevelAsync(int level);
        Task<Guid> CreateAsync(SopDetails sop);
        Task<bool> UpdateAsync(SopDetails sop);
        Task<bool> DeleteAsync(Guid id, Guid deletedBy);
        Task AddHistoryAsync(SopDetailsHistory history);
        Task AddApprovalHistoryAsync(SopDetailsApprovalHistory approval);
        Task<List<SopDetailsWorkFlowSetUp>> GetAllWorkflowStagesAsync();
        Task<SopDetailsWorkFlowSetUp?> GetWorkflowByLevelAsync(int level);
        Task<Guid> CreateWorkflowStageAsync(SopDetailsWorkFlowSetUp stage);
        Task<bool> UpdateWorkflowStageAsync(SopDetailsWorkFlowSetUp stage);
        Task<bool> DeleteWorkflowStageAsync(Guid id, Guid deletedBy);
        Task<bool> IsApproverForLevelAsync(Guid employeeId, int level);
        Task<List<int>> GetApproverLevelsForEmployeeAsync(Guid employeeId);
    }

    public class SopDetailDAL : ISopDetailDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<SopDetailDAL> _logger;

        public SopDetailDAL(IConfiguration configuration, ILogger<SopDetailDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        private async Task<SqlConnection> OpenAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        // ── GET ALL (paged, filtered) ──────────────────────────────────────────
        public async Task<(int totalCount, List<SopDetails>)> GetAllAsync(
            int pageNumber, int pageSize, string? search, int? status)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM SopDetails WITH(NOLOCK)
                WHERE IsDeleted = 0
                  AND (@Search IS NULL OR SopTitle LIKE '%' + @Search + '%')
                  AND (@Status IS NULL OR ApprovalStatus = @Status);

                SELECT ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                       Remark, ApprovalLevel, ApprovalStatus,
                       Version, IsActive, IsDeleted, Created, CreatedByID,
                       Modified, ModifiedByID, Deleted, DeletedByID
                FROM SopDetails WITH(NOLOCK)
                WHERE IsDeleted = 0
                  AND (@Search IS NULL OR SopTitle LIKE '%' + @Search + '%')
                  AND (@Status IS NULL OR ApprovalStatus = @Status)
                ORDER BY Created DESC
                OFFSET (@Page - 1) * @Size ROWS
                FETCH NEXT @Size ROWS ONLY;";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Search", string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : search);
            cmd.Parameters.AddWithValue("@Status", status.HasValue ? (object)status.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Page", pageNumber);
            cmd.Parameters.AddWithValue("@Size", pageSize);

            int total = 0;
            var list = new List<SopDetails>();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) total = reader.GetInt32(0);

            if (await reader.NextResultAsync())
                while (await reader.ReadAsync())
                    list.Add(MapSop(reader));

            return (total, list);
        }

        // ── GET BY ID ─────────────────────────────────────────────────────────
        public async Task<SopDetails?> GetByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                       Remark, ApprovalLevel, ApprovalStatus,
                       Version, IsActive, IsDeleted, Created, CreatedByID,
                       Modified, ModifiedByID, Deleted, DeletedByID
                FROM SopDetails WITH(NOLOCK)
                WHERE ID = @ID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", id);

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapSop(reader) : null;
        }

        // ── GET WITH HISTORY (returns SopDetails + populates histories) ───────
        public async Task<SopDetails?> GetWithHistoryAsync(Guid id)
        {
            var sop = await GetByIdAsync(id);
            return sop;
        }

        public async Task<List<SopDetailsHistory>> GetVersionHistoryAsync(Guid sopId)
        {
            const string sql = @"
                SELECT ID, SopDetailsID, Name, FileName, ApprovalStatus, ApprovalLevel,
                       ExpiryDate, Remarks, Version, IsActive, IsDeleted, Created, CreatedByID,
                       Modified, ModifiedByID, Deleted, DeletedByID
                FROM SopDetailsHistory WITH(NOLOCK)
                WHERE SopDetailsID = @SopID AND IsDeleted = 0
                ORDER BY Created DESC";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SopID", sopId);

            var list = new List<SopDetailsHistory>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new SopDetailsHistory
                {
                    ID = reader.GetGuid(reader.GetOrdinal("ID")),
                    SopDetailsID = reader.GetGuid(reader.GetOrdinal("SopDetailsID")),
                    Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? null : reader.GetString(reader.GetOrdinal("Name")),
                    FileName = reader.IsDBNull(reader.GetOrdinal("FileName")) ? null : reader.GetString(reader.GetOrdinal("FileName")),
                    ApprovalStatus = reader.GetInt32(reader.GetOrdinal("ApprovalStatus")),
                    ApprovalLevel = reader.GetInt32(reader.GetOrdinal("ApprovalLevel")),
                    ExpiryDate = reader.IsDBNull(reader.GetOrdinal("ExpiryDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ExpiryDate")),
                    Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                    Version = reader.GetInt32(reader.GetOrdinal("Version")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                    Created = reader.IsDBNull(reader.GetOrdinal("Created")) ? null : reader.GetDateTime(reader.GetOrdinal("Created")),
                    CreatedByID = reader.IsDBNull(reader.GetOrdinal("CreatedByID")) ? null : reader.GetGuid(reader.GetOrdinal("CreatedByID"))
                });

            return list;
        }

        public async Task<List<SopDetailsApprovalHistory>> GetApprovalHistoryAsync(Guid sopId)
        {
            const string sql = @"
                SELECT ID, SopDetailsID, ApprovalLevel, ApprovalStatus, Comments,
                       ReferenceVersion, Version, IsActive, IsDeleted, Created, CreatedByID,
                       Modified, ModifiedByID, Deleted, DeletedByID
                FROM SopDetailsApprovalHistory WITH(NOLOCK)
                WHERE SopDetailsID = @SopID AND IsDeleted = 0
                ORDER BY ApprovalLevel, Created";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SopID", sopId);

            var list = new List<SopDetailsApprovalHistory>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new SopDetailsApprovalHistory
                {
                    ID = reader.GetGuid(reader.GetOrdinal("ID")),
                    SopDetailsID = reader.GetGuid(reader.GetOrdinal("SopDetailsID")),
                    ApprovalLevel = reader.GetInt32(reader.GetOrdinal("ApprovalLevel")),
                    ApprovalStatus = reader.GetInt32(reader.GetOrdinal("ApprovalStatus")),
                    Comments = reader.IsDBNull(reader.GetOrdinal("Comments")) ? null : reader.GetString(reader.GetOrdinal("Comments")),
                    ReferenceVersion = reader.GetInt32(reader.GetOrdinal("ReferenceVersion")),
                    Version = reader.GetInt32(reader.GetOrdinal("Version")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                    Created = reader.IsDBNull(reader.GetOrdinal("Created")) ? null : reader.GetDateTime(reader.GetOrdinal("Created")),
                    CreatedByID = reader.IsDBNull(reader.GetOrdinal("CreatedByID")) ? null : reader.GetGuid(reader.GetOrdinal("CreatedByID"))
                });

            return list;
        }

        public async Task<List<SopDetails>> GetActiveSopsAsync()
        {
            const string sql = @"
                SELECT ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                       Remark, ApprovalLevel, ApprovalStatus,
                       Version, IsActive, IsDeleted, Created, CreatedByID,
                       Modified, ModifiedByID, Deleted, DeletedByID
                FROM SopDetails WITH(NOLOCK)
                WHERE IsDeleted = 0 AND IsActive = 1
                  AND (ExpirationDate IS NULL OR ExpirationDate > GETUTCDATE())
                ORDER BY Created DESC";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            var list = new List<SopDetails>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapSop(reader));
            return list;
        }

        public async Task<List<SopDetails>> GetByStatusAsync(int status)
        {
            const string sql = @"
                SELECT ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                       Remark, ApprovalLevel, ApprovalStatus,
                       Version, IsActive, IsDeleted, Created, CreatedByID,
                       Modified, ModifiedByID, Deleted, DeletedByID
                FROM SopDetails WITH(NOLOCK)
                WHERE IsDeleted = 0 AND ApprovalStatus = @Status
                ORDER BY Created DESC";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Status", status);
            var list = new List<SopDetails>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapSop(reader));
            return list;
        }

        public async Task<List<SopDetails>> GetByApprovalLevelAsync(int level)
        {
            // PendingApprovalLevel1=3, Level2=4, Level3=5
            int statusCode = level switch { 1 => 3, 2 => 4, 3 => 5, _ => 3 };
            return await GetByStatusAsync(statusCode);
        }

        // ── CREATE ────────────────────────────────────────────────────────────
        public async Task<Guid> CreateAsync(SopDetails sop)
        {
            sop.ID = Guid.NewGuid();
            const string sql = @"
                INSERT INTO SopDetails
                (ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                 Remark, ApprovalLevel, ApprovalStatus, CreatedByID)
                VALUES
                (@ID, @Title, @ExpDate, @Doc, @DocVersion,
                 @Remark, @Level, @Status, @CreatedBy)";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", sop.ID);
            cmd.Parameters.AddWithValue("@Title", sop.SopTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExpDate", sop.ExpirationDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Doc", sop.SopDocument ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DocVersion", sop.SopDocumentVersion);
            cmd.Parameters.AddWithValue("@Remark", sop.Remark ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Level", sop.ApprovalLevel);
            cmd.Parameters.AddWithValue("@Status", sop.ApprovalStatus);
            cmd.Parameters.AddWithValue("@CreatedBy", sop.CreatedByID);
            await cmd.ExecuteNonQueryAsync();
            return sop.ID;
        }

        // ── UPDATE ────────────────────────────────────────────────────────────
        public async Task<bool> UpdateAsync(SopDetails sop)
        {
            const string sql = @"
                UPDATE SopDetails
                SET SopTitle = @Title, ExpirationDate = @ExpDate,
                    SopDocument = @Doc, SopDocumentVersion = @DocVersion,
                    Remark = @Remark, ApprovalLevel = @Level, ApprovalStatus = @Status,
                    Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy,
                    Version = Version + 1
                WHERE ID = @ID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", sop.ID);
            cmd.Parameters.AddWithValue("@Title", sop.SopTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExpDate", sop.ExpirationDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Doc", sop.SopDocument ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DocVersion", sop.SopDocumentVersion);
            cmd.Parameters.AddWithValue("@Remark", sop.Remark ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Level", sop.ApprovalLevel);
            cmd.Parameters.AddWithValue("@Status", sop.ApprovalStatus);
            cmd.Parameters.AddWithValue("@ModifiedBy", sop.ModifiedByID);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ── SOFT DELETE ───────────────────────────────────────────────────────
        public async Task<bool> DeleteAsync(Guid id, Guid deletedBy)
        {
            const string sql = @"
                UPDATE SopDetails
                SET IsDeleted = 1, IsActive = 0, Deleted = GETUTCDATE(), DeletedByID = @DeletedBy
                WHERE ID = @ID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", id);
            cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ── ADD HISTORY ───────────────────────────────────────────────────────
        public async Task AddHistoryAsync(SopDetailsHistory history)
        {
            const string sql = @"
                INSERT INTO SopDetailsHistory
                (ID, SopDetailsID, Name, FileName, ApprovalStatus, ApprovalLevel,
                 ExpiryDate, Remarks, CreatedByID)
                VALUES
                (NEWID(), @SopID, @Name, @FileName, @Status, @Level,
                 @ExpiryDate, @Remarks, @CreatedBy)";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SopID", history.SopDetailsID);
            cmd.Parameters.AddWithValue("@Name", history.Name ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FileName", history.FileName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", history.ApprovalStatus);
            cmd.Parameters.AddWithValue("@Level", history.ApprovalLevel);
            cmd.Parameters.AddWithValue("@ExpiryDate", history.ExpiryDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Remarks", history.Remarks ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", history.CreatedByID);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── ADD APPROVAL HISTORY ──────────────────────────────────────────────
        public async Task AddApprovalHistoryAsync(SopDetailsApprovalHistory approval)
        {
            const string sql = @"
                INSERT INTO SopDetailsApprovalHistory
                (ID, SopDetailsID, ApprovalLevel, ApprovalStatus, Comments,
                 ReferenceVersion, CreatedByID)
                VALUES
                (NEWID(), @SopID, @Level, @Status, @Comments, @RefVersion, @CreatedBy)";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SopID", approval.SopDetailsID);
            cmd.Parameters.AddWithValue("@Level", approval.ApprovalLevel);
            cmd.Parameters.AddWithValue("@Status", approval.ApprovalStatus);
            cmd.Parameters.AddWithValue("@Comments", approval.Comments ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RefVersion", approval.ReferenceVersion);
            cmd.Parameters.AddWithValue("@CreatedBy", approval.CreatedByID);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── WORKFLOW STAGES ───────────────────────────────────────────────────
        public async Task<List<SopDetailsWorkFlowSetUp>> GetAllWorkflowStagesAsync()
        {
            const string sql = @"
                SELECT wf.ID, wf.StageName, wf.ApprovalLevel, wf.IsSupervisor,
                       wf.EmployeeGroupID, eg.Name AS GroupName,
                       wf.Version, wf.IsActive, wf.IsDeleted, wf.Created, wf.CreatedByID,
                       wf.Modified, wf.ModifiedByID, wf.Deleted, wf.DeletedByID
                FROM SopDetailsWorkFlowSetUp wf WITH(NOLOCK)
                JOIN EmployeeGroup eg WITH(NOLOCK) ON eg.ID = wf.EmployeeGroupID
                WHERE wf.IsDeleted = 0
                ORDER BY wf.ApprovalLevel";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            var list = new List<SopDetailsWorkFlowSetUp>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapWorkflow(reader));
            return list;
        }

        public async Task<SopDetailsWorkFlowSetUp?> GetWorkflowByLevelAsync(int level)
        {
            const string sql = @"
                SELECT wf.ID, wf.StageName, wf.ApprovalLevel, wf.IsSupervisor,
                       wf.EmployeeGroupID, eg.Name AS GroupName,
                       wf.Version, wf.IsActive, wf.IsDeleted, wf.Created, wf.CreatedByID,
                       wf.Modified, wf.ModifiedByID, wf.Deleted, wf.DeletedByID
                FROM SopDetailsWorkFlowSetUp wf WITH(NOLOCK)
                JOIN EmployeeGroup eg WITH(NOLOCK) ON eg.ID = wf.EmployeeGroupID
                WHERE wf.ApprovalLevel = @Level AND wf.IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Level", level);
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapWorkflow(reader) : null;
        }

        public async Task<Guid> CreateWorkflowStageAsync(SopDetailsWorkFlowSetUp stage)
        {
            stage.ID = Guid.NewGuid();
            const string sql = @"
                INSERT INTO SopDetailsWorkFlowSetUp
                (ID, StageName, ApprovalLevel, IsSupervisor, EmployeeGroupID, CreatedByID)
                VALUES
                (@ID, @Name, @Level, @IsSup, @GroupID, @CreatedBy)";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", stage.ID);
            cmd.Parameters.AddWithValue("@Name", stage.StageName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Level", stage.ApprovalLevel);
            cmd.Parameters.AddWithValue("@IsSup", stage.IsSupervisor);
            cmd.Parameters.AddWithValue("@GroupID", stage.EmployeeGroupID);
            cmd.Parameters.AddWithValue("@CreatedBy", stage.CreatedByID);
            await cmd.ExecuteNonQueryAsync();
            return stage.ID;
        }

        public async Task<bool> UpdateWorkflowStageAsync(SopDetailsWorkFlowSetUp stage)
        {
            const string sql = @"
                UPDATE SopDetailsWorkFlowSetUp
                SET StageName = @Name, ApprovalLevel = @Level,
                    IsSupervisor = @IsSup, EmployeeGroupID = @GroupID,
                    Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy,
                    Version = Version + 1
                WHERE ID = @ID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", stage.ID);
            cmd.Parameters.AddWithValue("@Name", stage.StageName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Level", stage.ApprovalLevel);
            cmd.Parameters.AddWithValue("@IsSup", stage.IsSupervisor);
            cmd.Parameters.AddWithValue("@GroupID", stage.EmployeeGroupID);
            cmd.Parameters.AddWithValue("@ModifiedBy", stage.ModifiedByID);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteWorkflowStageAsync(Guid id, Guid deletedBy)
        {
            const string sql = @"
                UPDATE SopDetailsWorkFlowSetUp
                SET IsDeleted = 1, IsActive = 0,
                    Deleted = GETUTCDATE(), DeletedByID = @DeletedBy
                WHERE ID = @ID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", id);
            cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> IsApproverForLevelAsync(Guid employeeId, int level)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM SopDetailsWorkFlowSetUp wf WITH(NOLOCK)
                JOIN EmployeeGroupDetail egd WITH(NOLOCK) ON egd.EmployeeGroupID = wf.EmployeeGroupID
                WHERE egd.EmployeeID = @EmpID
                  AND wf.ApprovalLevel = @Level
                  AND wf.IsActive = 1 AND wf.IsDeleted = 0
                  AND egd.IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);
            cmd.Parameters.AddWithValue("@Level", level);
            return (int)(await cmd.ExecuteScalarAsync())! > 0;
        }

        public async Task<List<int>> GetApproverLevelsForEmployeeAsync(Guid employeeId)
        {
            const string sql = @"
                SELECT DISTINCT wf.ApprovalLevel
                FROM SopDetailsWorkFlowSetUp wf WITH(NOLOCK)
                JOIN EmployeeGroupDetail egd WITH(NOLOCK) ON egd.EmployeeGroupID = wf.EmployeeGroupID
                WHERE egd.EmployeeID = @EmpID
                  AND wf.IsDeleted = 0 AND wf.IsActive = 1
                  AND egd.IsDeleted = 0
                ORDER BY wf.ApprovalLevel";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);
            var list = new List<int>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(reader.GetInt32(0));
            return list;
        }

        // ── PRIVATE MAPPERS ───────────────────────────────────────────────────
        private static SopDetails MapSop(SqlDataReader r) => new()
        {
            ID = r.GetGuid(r.GetOrdinal("ID")),
            SopTitle = r.IsDBNull(r.GetOrdinal("SopTitle")) ? null : r.GetString(r.GetOrdinal("SopTitle")),
            ExpirationDate = r.IsDBNull(r.GetOrdinal("ExpirationDate")) ? null : r.GetDateTime(r.GetOrdinal("ExpirationDate")),
            SopDocument = r.IsDBNull(r.GetOrdinal("SopDocument")) ? null : r.GetString(r.GetOrdinal("SopDocument")),
            SopDocumentVersion = r.GetInt32(r.GetOrdinal("SopDocumentVersion")),
            Remark = r.IsDBNull(r.GetOrdinal("Remark")) ? null : r.GetString(r.GetOrdinal("Remark")),
            ApprovalLevel = r.GetInt32(r.GetOrdinal("ApprovalLevel")),
            ApprovalStatus = r.GetInt32(r.GetOrdinal("ApprovalStatus")),
            Version = r.GetInt32(r.GetOrdinal("Version")),
            IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
            IsDeleted = r.GetBoolean(r.GetOrdinal("IsDeleted")),
            Created = r.IsDBNull(r.GetOrdinal("Created")) ? null : r.GetDateTime(r.GetOrdinal("Created")),
            CreatedByID = r.IsDBNull(r.GetOrdinal("CreatedByID")) ? null : r.GetGuid(r.GetOrdinal("CreatedByID")),
            Modified = r.IsDBNull(r.GetOrdinal("Modified")) ? null : r.GetDateTime(r.GetOrdinal("Modified")),
            ModifiedByID = r.IsDBNull(r.GetOrdinal("ModifiedByID")) ? null : r.GetGuid(r.GetOrdinal("ModifiedByID")),
            Deleted = r.IsDBNull(r.GetOrdinal("Deleted")) ? null : r.GetDateTime(r.GetOrdinal("Deleted")),
            DeletedByID = r.IsDBNull(r.GetOrdinal("DeletedByID")) ? null : r.GetGuid(r.GetOrdinal("DeletedByID"))
        };

        private static SopDetailsWorkFlowSetUp MapWorkflow(SqlDataReader r) => new()
        {
            ID = r.GetGuid(r.GetOrdinal("ID")),
            StageName = r.IsDBNull(r.GetOrdinal("StageName")) ? null : r.GetString(r.GetOrdinal("StageName")),
            ApprovalLevel = r.GetInt32(r.GetOrdinal("ApprovalLevel")),
            IsSupervisor = r.GetBoolean(r.GetOrdinal("IsSupervisor")),
            EmployeeGroupID = r.GetGuid(r.GetOrdinal("EmployeeGroupID")),
            GroupName = r.IsDBNull(r.GetOrdinal("GroupName")) ? null : r.GetString(r.GetOrdinal("GroupName")),
            Version = r.GetInt32(r.GetOrdinal("Version")),
            IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
            IsDeleted = r.GetBoolean(r.GetOrdinal("IsDeleted")),
            Created = r.IsDBNull(r.GetOrdinal("Created")) ? null : r.GetDateTime(r.GetOrdinal("Created")),
            CreatedByID = r.IsDBNull(r.GetOrdinal("CreatedByID")) ? null : r.GetGuid(r.GetOrdinal("CreatedByID"))
        };
    }
}