using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TecLogos.SOP.DataModel.SOP;
using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.DAL.SOP
{
    public interface ISopDetailDAL
    {
        Task<Guid> CreateSop(SopDetail sop);
        Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments);
        Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments);
        Task<List<SopTrackingStep>> GetSopTracking(Guid sopId);
        Task<(int totalCount, List<SopDetail>)> GetSopsForApproval(Guid loggedUserId, int approvalStatus, int year);
        Task<(int totalCount, List<SopApprovalHistory>)> GetSopsHistory(Guid userId, int approvalStatus, int? year);
        Task<(int totalCount, List<SopDetail>)> GetMySopsHistory(Guid userId, int approvalStatus, int? year);
        Task<(int totalCount, List<SopDetail>)> GetAllSops(int? approvalStatus, int? year);
        Task<bool> IsUserApprover(Guid userId);
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

        private SqlConnection CreateConnection() => new(_connectionString);


        // ─────────────────────────────────────────────
        //  CREATE SOP
        //  Inserts into SopDetails + SopDetailsHistory
        //  ApprovalLevel = 0 (Not Started)
        //  ApprovalStatus = 0 (Pending)
        // ─────────────────────────────────────────────
        public async Task<Guid> CreateSop(SopDetail sop)
        {
            const string sql = @"
                INSERT INTO [SopDetails]
                    (ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                     Remark, ApprovalLevel, ApprovalStatus, CreatedByID, Created)
                VALUES
                    (@ID, @SopTitle, @ExpirationDate, @SopDocument, 1,
                     @Remark, 0, 0, @CreatedByID, GETDATE());

                INSERT INTO [SopDetailsHistory]
                    (SopDetailsID, SopTitle, ExpirationDate, SopDocument,
                     SopDocumentVersion, Remark, ApprovalLevel, ApprovalStatus,
                     CreatedByID, Created)
                VALUES
                    (@ID, @SopTitle, @ExpirationDate, @SopDocument,
                     1, @Remark, 0, 0, @CreatedByID, GETDATE());";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = sop.ID;
            cmd.Parameters.Add("@SopTitle", SqlDbType.NVarChar).Value = (object?)sop.SopTitle ?? DBNull.Value;
            cmd.Parameters.Add("@ExpirationDate", SqlDbType.DateTime2).Value = (object?)sop.ExpirationDate ?? DBNull.Value;
            cmd.Parameters.Add("@SopDocument", SqlDbType.NVarChar).Value = (object?)sop.SopDocument ?? DBNull.Value;
            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar).Value = (object?)sop.Remark ?? DBNull.Value;
            cmd.Parameters.Add("@CreatedByID", SqlDbType.UniqueIdentifier).Value = sop.CreatedByID;

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("SOP created. ID={SopId} By={EmpId}", sop.ID, sop.CreatedByID);
            return sop.ID;
        }


        // ─────────────────────────────────────────────
        //  APPROVE SOP  (one-shot query pattern)
        //
        //  Workflow levels: 0=NotStarted 1=InProgress
        //    2=Submitted 3=L1 4=L2 5=L3(max)
        //
        //  Logic:
        //    1. Lock + read current ApprovalLevel from SopDetails
        //    2. Get MaxLevel from WorkFlowSetUp
        //    3. NextLevel = CurrentLevel + 1
        //    4. Verify approver is authorised at NextLevel
        //       - IsSupervisor=1 → check Employee.ManagerID = approverId
        //       - IsSupervisor=0 → check EmployeeGroupDetail membership
        //    5. Advance ApprovalLevel to NextLevel
        //       - If NextLevel >= MaxLevel → ApprovalStatus = 3 (Completed)
        //       - Otherwise ApprovalStatus stays 0 (Pending)
        //    6. Write snapshot to SopDetailsApprovalHistory
        // ─────────────────────────────────────────────
        public async Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments)
        {
            const string sql = @"
                DECLARE @MaxLevel     INT;
                DECLARE @CurrentLevel INT;
                DECLARE @NextLevel    INT;

                -- 1. Lock the SOP row and read current level
                --    Only act on Pending SOPs (ApprovalStatus = 0)
                SELECT @CurrentLevel = ApprovalLevel
                FROM   [SopDetails] WITH (UPDLOCK, ROWLOCK)
                WHERE  ID            = @SopID
                  AND  IsDeleted     = 0
                  AND  ApprovalStatus = 0;

                IF @CurrentLevel IS NULL RETURN;  -- not found / already closed

                -- 2. Get max approval level from workflow definition
                SELECT @MaxLevel = MAX(ApprovalLevel)
                FROM   [SopDetailsWorkFlowSetUp]
                WHERE  IsDeleted = 0;

                SET @NextLevel = @CurrentLevel + 1;

                -- 3. Authorisation check for NextLevel
                --    Either group-based OR supervisor (manager-based)
                IF NOT EXISTS (
                    SELECT 1
                    FROM   [SopDetailsWorkFlowSetUp] WF
                    LEFT JOIN [EmployeeGroupDetail]  EGD
                           ON EGD.EmployeeGroupID = WF.EmployeeGroupID
                          AND EGD.EmployeeID      = @ApproverID
                          AND EGD.IsDeleted       = 0
                    WHERE  WF.ApprovalLevel = @NextLevel
                      AND  WF.IsDeleted     = 0
                      AND (
                              -- Group-based approver (L1/L2/L3)
                              (WF.IsSupervisor = 0 AND EGD.EmployeeID = @ApproverID)
                           OR
                              -- Supervisor: approver must be direct manager of SOP creator
                              (WF.IsSupervisor = 1
                               AND EXISTS (
                                   SELECT 1
                                   FROM   [SopDetails]  SD
                                   INNER JOIN [Employee] EMP ON EMP.ID = SD.CreatedByID
                                   WHERE  SD.ID          = @SopID
                                     AND  EMP.ManagerID  = @ApproverID))
                          )
                )
                    RETURN;   -- approver not authorised at this level

                -- 4. Advance SOP
                --    NextLevel >= MaxLevel → Completed (3), else stay Pending (0)
                UPDATE [SopDetails]
                SET    ApprovalLevel  = @NextLevel,
                       ApprovalStatus = IIF(@NextLevel >= @MaxLevel, 3, 0),
                       Modified       = GETDATE(),
                       ModifiedByID   = @ApproverID
                WHERE  ID = @SopID;

                -- 5. Write approval history row
                INSERT INTO [SopDetailsApprovalHistory]
                    (ID, SopDetailsID, ApprovalLevel, ApprovalStatus,
                     Comments, ReferenceVersion, CreatedByID, Created)
                SELECT
                    NEWID(), @SopID, @NextLevel,
                    IIF(@NextLevel >= @MaxLevel, 3, 1),  -- 3=Completed, 1=Approved
                    @Comments,
                    SD.Version,
                    @ApproverID, GETDATE()
                FROM [SopDetails] SD
                WHERE SD.ID = @SopID;";

            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            using var cmd = new SqlCommand(sql, conn, tx);

            cmd.Parameters.Add("@SopID", SqlDbType.UniqueIdentifier).Value = sopId;
            cmd.Parameters.Add("@ApproverID", SqlDbType.UniqueIdentifier).Value = approverId;
            cmd.Parameters.Add("@Comments", SqlDbType.NVarChar).Value = (object?)comments ?? DBNull.Value;

            var rows = await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();

            _logger.LogInformation("SOP {SopId} approved by {ApproverId}", sopId, approverId);
            return rows > 0;
        }


        // ─────────────────────────────────────────────
        //  REJECT SOP  (one-shot query pattern)
        //
        //  Logic:
        //    1. Update SopDetails: ApprovalStatus = 2 (Rejected)
        //       Only if currently Pending (ApprovalStatus = 0)
        //    2. Read current level
        //    3. Write rejection row to SopDetailsApprovalHistory
        //    Note: Rejected SOP returns to initiator (Level 1)
        //          Frontend handles reassignment — DB records the rejection
        // ─────────────────────────────────────────────
        public async Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments)
        {
            const string sql = @"
                DECLARE @CurrentLevel INT;

                -- 1. Reject only Pending SOPs
                UPDATE [SopDetails]
                SET    ApprovalStatus = 2,       -- 2 = Rejected
                       Modified       = GETDATE(),
                       ModifiedByID   = @ApproverID
                WHERE  ID             = @SopID
                  AND  IsDeleted      = 0
                  AND  ApprovalStatus = 0;        -- only Pending

                IF @@ROWCOUNT = 0 RETURN;         -- already approved/rejected/completed

                -- 2. Read level at time of rejection
                SELECT @CurrentLevel = ApprovalLevel
                FROM   [SopDetails]
                WHERE  ID = @SopID;

                -- 3. Write rejection history row
                INSERT INTO [SopDetailsApprovalHistory]
                    (ID, SopDetailsID, ApprovalLevel, ApprovalStatus,
                     Comments, ReferenceVersion, CreatedByID, Created)
                SELECT
                    NEWID(), @SopID, @CurrentLevel,
                    2,              -- 2 = Rejected
                    @Comments,
                    SD.Version,
                    @ApproverID, GETDATE()
                FROM [SopDetails] SD
                WHERE SD.ID = @SopID;";

            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            using var cmd = new SqlCommand(sql, conn, tx);

            cmd.Parameters.Add("@SopID", SqlDbType.UniqueIdentifier).Value = sopId;
            cmd.Parameters.Add("@ApproverID", SqlDbType.UniqueIdentifier).Value = approverId;
            cmd.Parameters.Add("@Comments", SqlDbType.NVarChar).Value = (object?)comments ?? DBNull.Value;

            var rows = await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();

            _logger.LogInformation("SOP {SopId} rejected by {ApproverId}", sopId, approverId);
            return rows > 0;
        }


        // ─────────────────────────────────────────────
        //  GET SOP TRACKING  (dual result-set pattern)
        //
        //  Result Set 1 → All workflow stages (structure)
        //                 from SopDetailsWorkFlowSetUp
        //  Result Set 2 → Actioned history rows
        //                 from SopDetailsApprovalHistory
        //  Merged in C# by ApprovalLevel key
        // ─────────────────────────────────────────────
        public async Task<List<SopTrackingStep>> GetSopTracking(Guid sopId)
        {
            const string sql = @"
                -- Result Set 1: full workflow structure (all 6 stages)
                SELECT
                    WF.ID,
                    WF.StageName,
                    WF.ApprovalLevel,
                    WF.IsSupervisor
                FROM  [SopDetailsWorkFlowSetUp] WF
                WHERE WF.IsDeleted = 0
                ORDER BY WF.ApprovalLevel;

                -- Result Set 2: what actually happened for this SOP
                -- AH.Created     = timestamp when action was taken
                -- Employee.Email = who took the action (via AH.CreatedByID)
                SELECT
                    AH.ApprovalLevel,
                    AH.ApprovalStatus,
                    AH.Comments,
                    AH.Created   AS ActionedOn,
                    EMP.Email    AS ActionedByEmail
                FROM  [SopDetailsApprovalHistory] AH
                INNER JOIN [Employee] EMP ON EMP.ID = AH.CreatedByID
                WHERE AH.SopDetailsID = @SopID
                  AND AH.IsDeleted    = 0
                ORDER BY AH.ApprovalLevel;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@SopID", SqlDbType.UniqueIdentifier).Value = sopId;

            var steps = new List<SopTrackingStep>();
            var historyMap = new Dictionary<int, (int? Status, DateTime? ActionedOn, string? Email, string? Comments)>();

            using var reader = await cmd.ExecuteReaderAsync();

            // Result Set 1: structure — action fields null until merged
            while (await reader.ReadAsync())
            {
                steps.Add(new SopTrackingStep
                {
                    ID = reader.GetGuid(reader.GetOrdinal("ID")),
                    StageName = ReadString(reader, "StageName"),
                    ApprovalLevel = reader.GetInt32(reader.GetOrdinal("ApprovalLevel")),
                    IsSupervisor = reader.GetBoolean(reader.GetOrdinal("IsSupervisor")),
                    ApprovalStatus = null,
                    ActionedOn = null,
                    ActionedByEmail = null,
                    Comments = null
                });
            }

            // Result Set 2: history rows keyed by ApprovalLevel
            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    int level = reader.GetInt32(reader.GetOrdinal("ApprovalLevel"));
                    historyMap[level] = (
                        ReadInt(reader, "ApprovalStatus"),
                        ReadDateTime(reader, "ActionedOn"),
                        ReadString(reader, "ActionedByEmail"),
                        ReadString(reader, "Comments")
                    );
                }
            }

            // Merge: only stages that have been actioned get status filled
            foreach (var step in steps)
            {
                if (historyMap.TryGetValue(step.ApprovalLevel, out var h))
                {
                    step.ApprovalStatus = h.Status.HasValue
                                            ? (SopApprovalStatus?)(SopApprovalStatus)h.Status.Value
                                            : null;
                    step.ActionedOn = h.ActionedOn;
                    step.ActionedByEmail = h.Email;
                    step.Comments = h.Comments;
                }
            }

            return steps;
        }


        // ─────────────────────────────────────────────
        //  GET SOPs FOR APPROVAL
        //
        //  Returns SOPs where logged user is the NEXT
        //  required actor at (CurrentLevel + 1):
        //    - IsSupervisor=1 → user is ManagerID of SOP creator
        //    - IsSupervisor=0 → user is in the group for that level
        //  Only Pending SOPs (ApprovalStatus = 0)
        // ─────────────────────────────────────────────
        public async Task<(int totalCount, List<SopDetail>)> GetSopsForApproval(
            Guid loggedUserId, int approvalStatus, int year)
        {
            const string sql = @"
                SELECT DISTINCT
                    SD.ID,
                    SD.SopTitle,
                    SD.ExpirationDate,
                    SD.SopDocument,
                    SD.SopDocumentVersion,
                    SD.Remark,
                    SD.ApprovalLevel  AS CurrentApprovalLevel,
                    SD.ApprovalStatus,
                    SD.Version,
                    SD.IsActive,
                    SD.IsDeleted,
                    SD.Created,
                    SD.CreatedByID
                FROM  [SopDetails] SD
                INNER JOIN [Employee] EMP
                       ON  EMP.ID       = SD.CreatedByID
                      AND  EMP.IsDeleted = 0
                INNER JOIN [SopDetailsWorkFlowSetUp] WF
                       ON  WF.ApprovalLevel = SD.ApprovalLevel + 1
                      AND  WF.IsDeleted     = 0
                LEFT  JOIN [EmployeeGroupDetail] EGD
                       ON  EGD.EmployeeGroupID = WF.EmployeeGroupID
                      AND  EGD.EmployeeID      = @LoggedUserID
                      AND  EGD.IsDeleted       = 0
                WHERE SD.IsDeleted      = 0
                  AND SD.ApprovalStatus = 0    -- Pending only
                  AND (
                          -- Group-based: L1/L2/L3 approvers
                          (WF.IsSupervisor = 0 AND EGD.EmployeeID = @LoggedUserID)
                       OR
                          -- Supervisor: direct manager of SOP creator
                          (WF.IsSupervisor = 1 AND EMP.ManagerID  = @LoggedUserID)
                      )
                  AND (@Year = 0 OR YEAR(SD.Created) = @Year)
                ORDER BY SD.Created;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@LoggedUserID", SqlDbType.UniqueIdentifier).Value = loggedUserId;
            cmd.Parameters.Add("@ApprovalStatus", SqlDbType.Int).Value = approvalStatus;
            cmd.Parameters.Add("@Year", SqlDbType.Int).Value = year;

            var list = new List<SopDetail>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapSopDetail(reader, levelColumn: "CurrentApprovalLevel"));

            return (list.Count, list);
        }


        // ─────────────────────────────────────────────
        //  GET SOPs HISTORY  (actions I have taken)
        //
        //  Returns rows from SopDetailsApprovalHistory
        //  where CreatedByID = logged user.
        //  Joined to SopDetails for title/document context.
        // ─────────────────────────────────────────────
        public async Task<(int totalCount, List<SopApprovalHistory>)> GetSopsHistory(
            Guid userId, int approvalStatus, int? year)
        {
            const string sql = @"
                SELECT DISTINCT
                    AH.ID,
                    AH.SopDetailsID,
                    AH.ApprovalLevel,
                    AH.ApprovalStatus,
                    AH.Comments,
                    AH.ReferenceVersion,
                    AH.CreatedByID,
                    AH.Created,
                    AH.IsDeleted
                FROM  [SopDetailsApprovalHistory] AH
                INNER JOIN [SopDetails] SD ON SD.ID = AH.SopDetailsID
                WHERE AH.IsDeleted    = 0
                  AND SD.IsDeleted    = 0
                  AND AH.CreatedByID  = @UserID
                  AND (@ApprovalStatus = 0 OR AH.ApprovalStatus = @ApprovalStatus)
                  AND (@Year = 0 OR YEAR(AH.Created) = @Year)
                ORDER BY AH.Created DESC;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@UserID", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@ApprovalStatus", SqlDbType.Int).Value = approvalStatus;
            cmd.Parameters.Add("@Year", SqlDbType.Int).Value = year ?? 0;

            var list = new List<SopApprovalHistory>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SopApprovalHistory
                {
                    ID = reader.GetGuid(reader.GetOrdinal("ID")),
                    SopDetailsID = reader.GetGuid(reader.GetOrdinal("SopDetailsID")),
                    ApprovalLevel = reader.GetInt32(reader.GetOrdinal("ApprovalLevel")),
                    ApprovalStatus = (SopApprovalStatus)reader.GetInt32(reader.GetOrdinal("ApprovalStatus")),
                    Comments = ReadString(reader, "Comments"),
                    ReferenceVersion = reader.GetInt32(reader.GetOrdinal("ReferenceVersion")),
                    CreatedByID = reader.GetGuid(reader.GetOrdinal("CreatedByID")),
                    Created = reader.GetDateTime(reader.GetOrdinal("Created")),
                    IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted"))
                });
            }

            return (list.Count, list);
        }


        // ─────────────────────────────────────────────
        //  GET MY SOPs HISTORY  (SOPs I created)
        //
        //  Returns SopDetails rows where CreatedByID = userId.
        //  Shows current stage/status of each SOP I submitted.
        // ─────────────────────────────────────────────
        public async Task<(int totalCount, List<SopDetail>)> GetMySopsHistory(
            Guid userId, int approvalStatus, int? year)
        {
            const string sql = @"
                SELECT
                    SD.ID,
                    SD.SopTitle,
                    SD.ExpirationDate,
                    SD.SopDocument,
                    SD.SopDocumentVersion,
                    SD.Remark,
                    SD.ApprovalLevel,
                    SD.ApprovalStatus,
                    SD.Version,
                    SD.IsActive,
                    SD.IsDeleted,
                    SD.Created,
                    SD.CreatedByID
                FROM  [SopDetails] SD
                WHERE SD.IsDeleted    = 0
                  AND SD.CreatedByID  = @UserID
                  AND (@ApprovalStatus = 0 OR SD.ApprovalStatus = @ApprovalStatus)
                  AND (@Year = 0 OR YEAR(SD.Created) = @Year)
                ORDER BY SD.Created DESC;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@UserID", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@ApprovalStatus", SqlDbType.Int).Value = approvalStatus;
            cmd.Parameters.Add("@Year", SqlDbType.Int).Value = year ?? 0;

            var list = new List<SopDetail>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapSopDetail(reader, levelColumn: "ApprovalLevel"));

            return (list.Count, list);
        }


        // ─────────────────────────────────────────────
        //  GET ALL SOPs  (admin/dashboard view)
        //
        //  Returns all non-deleted SOPs.
        //  Optionally filtered by ApprovalStatus and/or Year.
        //  NULL filters = return everything.
        // ─────────────────────────────────────────────
        public async Task<(int totalCount, List<SopDetail>)> GetAllSops(
            int? approvalStatus, int? year)
        {
            const string sql = @"
                SELECT
                    SD.ID,
                    SD.SopTitle,
                    SD.ExpirationDate,
                    SD.SopDocument,
                    SD.SopDocumentVersion,
                    SD.Remark,
                    SD.ApprovalLevel,
                    SD.ApprovalStatus,
                    SD.Version,
                    SD.IsActive,
                    SD.IsDeleted,
                    SD.Created,
                    SD.CreatedByID
                FROM  [SopDetails] SD
                WHERE SD.IsDeleted = 0
                  AND (@ApprovalStatus IS NULL OR SD.ApprovalStatus = @ApprovalStatus)
                  AND (@Year         IS NULL OR YEAR(SD.Created)   = @Year)
                ORDER BY SD.Created DESC;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ApprovalStatus", SqlDbType.Int).Value = (object?)approvalStatus ?? DBNull.Value;
            cmd.Parameters.Add("@Year", SqlDbType.Int).Value = (object?)year ?? DBNull.Value;

            var list = new List<SopDetail>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapSopDetail(reader, levelColumn: "ApprovalLevel"));

            return (list.Count, list);
        }


        // ─────────────────────────────────────────────
        //  IS USER APPROVER
        //
        //  Check 1: group-based → in any non-supervisor
        //           workflow group
        //  Check 2: supervisor  → has direct reports
        // ─────────────────────────────────────────────
        public async Task<bool> IsUserApprover(Guid userId)
        {
            const string groupSql = @"
                SELECT COUNT(1)
                FROM   [SopDetailsWorkFlowSetUp] WF
                JOIN   [EmployeeGroupDetail]     EGD
                       ON  EGD.EmployeeGroupID = WF.EmployeeGroupID
                      AND  EGD.IsDeleted       = 0
                WHERE  EGD.EmployeeID  = @UserId
                  AND  WF.IsDeleted    = 0
                  AND  WF.IsSupervisor = 0;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd1 = new SqlCommand(groupSql, conn);
            cmd1.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            var groupCount = (int)(await cmd1.ExecuteScalarAsync())!;
            if (groupCount > 0) return true;

            const string supervisorSql = @"
                SELECT COUNT(1) FROM [Employee]
                WHERE ManagerID = @UserId AND IsDeleted = 0;";

            using var cmd2 = new SqlCommand(supervisorSql, conn);
            cmd2.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            var reporteeCount = (int)(await cmd2.ExecuteScalarAsync())!;
            return reporteeCount > 0;
        }


        // ─────────────────────────────────────────────
        //  PRIVATE: Map SopDetail from reader
        //  Only columns that exist in [SopDetails]
        // ─────────────────────────────────────────────
        private static SopDetail MapSopDetail(SqlDataReader r, string levelColumn)
            => new()
            {
                ID = r.GetGuid(r.GetOrdinal("ID")),
                SopTitle = ReadString(r, "SopTitle"),
                ExpirationDate = ReadDateTime(r, "ExpirationDate"),
                SopDocument = ReadString(r, "SopDocument"),
                SopDocumentVersion = r.GetInt32(r.GetOrdinal("SopDocumentVersion")),
                Remark = ReadString(r, "Remark"),
                ApprovalLevel = ReadInt(r, levelColumn) ?? 0,
                ApprovalStatus = ReadEnum<SopApprovalStatus>(r, "ApprovalStatus"),
                Version = r.GetInt32(r.GetOrdinal("Version")),
                IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
                IsDeleted = r.GetBoolean(r.GetOrdinal("IsDeleted")),
                Created = r.GetDateTime(r.GetOrdinal("Created")),
                CreatedByID = r.GetGuid(r.GetOrdinal("CreatedByID"))
            };


        // ─────────────────────────────────────────────
        //  PRIVATE: Null-safe reader helpers
        // ─────────────────────────────────────────────
        private static string? ReadString(SqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetString(o); }
        private static DateTime? ReadDateTime(SqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetDateTime(o); }
        private static int? ReadInt(SqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetInt32(o); }

        // Two-step cast: int → T (enum), then implicit T → T?
        // Direct (T?)(object)int fails at runtime — CLR cannot unbox int to Nullable<T>
        private static T? ReadEnum<T>(SqlDataReader r, string col) where T : struct, Enum
        {
            var o = r.GetOrdinal(col);
            if (r.IsDBNull(o)) return null;
            var intValue = r.GetInt32(o);       // step 1: unbox to int
            return (T)(object)intValue;          // step 2: int → enum (T? wraps implicitly)
        }
    }
}