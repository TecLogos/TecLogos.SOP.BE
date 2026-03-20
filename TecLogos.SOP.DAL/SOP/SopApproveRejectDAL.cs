using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using TecLogos.SOP.DataModel.SOP;
using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.DAL.SOP
{
    public interface ISopApproveRejectDAL
    {
        Task<(int totalCount, List<SopDetail>)> GetSopsForApproval(Guid loggedUserId, int approvalStatus, int year);
        Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments, int approvalLevel);
        Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments);

    }
    public class SopApproveRejectDAL : ISopApproveRejectDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<SopApproveRejectDAL> _logger;

        public SopApproveRejectDAL(IConfiguration configuration, ILogger<SopApproveRejectDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        private SqlConnection CreateConnection() => new(_connectionString);

        public async Task<(int totalCount, List<SopDetail>)> GetSopsForApproval(Guid loggedUserId, int approvalStatus, int year)
        {
            const string sql =
                @"
                    SELECT SD.ID, SD.SopTitle, SD.ExpirationDate, SD.SopDocument, SD.SopDocumentVersion, SD.EmployeeID, WF.ApprovalLevel [NextApprovalLevel], SD.ApprovalLevel, WFL.StageName, ISNULL(WF.StageName, '') NextStageName, SD.ApprovalStatus, SD.Version
                  FROM [SopDetails] SD
                      INNER JOIN Employee EMP WITH(NOLOCK) ON EMP.ID = SD.EmployeeID AND EMP.IsDeleted = 0
                      INNER JOIN [SopDetailsWorkFlowSetUp] WF WITH(NOLOCK) ON  WF.ApprovalLevel = SD.ApprovalLevel + 1
                      LEFT JOIN SopDetailsWorkFlowSetUp WFL WITH(NOLOCK) ON SD.ApprovalLevel = WFL.ApprovalLevel AND WFL.IsDeleted = 0
                   WHERE SD.IsDeleted = 0 AND WF.IsDeleted = 0
                      AND (WF.IsSupervisor = 1 AND EMP.ManagerID = @LoggedUserID)  --  Supervisors list
                  
                  UNION

                  SELECT SD.ID, SD.SopTitle, SD.ExpirationDate, SD.SopDocument, SD.SopDocumentVersion, SD.EmployeeID, WF.ApprovalLevel [NextApprovalLevel], SD.ApprovalLevel, WFL.StageName, ISNULL(WF.StageName, '') NextStageName, SD.ApprovalStatus, SD.Version
                  FROM [SopDetails] SD
                      INNER JOIN [SopDetailsWorkFlowSetUp] WF WITH(NOLOCK) ON  WF.ApprovalLevel = SD.ApprovalLevel + 1
                      INNER JOIN EmployeeGroup EG WITH(NOLOCK) ON EG.ID = WF.EmployeeGroupID
                      INNER JOIN EmployeeGroupDetail EGD WITH(NOLOCK) ON EGD.EmployeeGroupID = EG.ID AND EGD.EmployeeID = @LoggedUserID AND EGD.IsDeleted = 0 
                      LEFT JOIN SopDetailsWorkFlowSetUp WFL WITH(NOLOCK) ON SD.ApprovalLevel = WFL.ApprovalLevel AND WFL.IsDeleted = 0
                   WHERE SD.IsDeleted = 0 AND WF.IsDeleted = 0
                         AND (WF.IsSupervisor = 0 AND WF.EmployeeGroupID = EG.ID)  --  Approval group wise
                  ORDER BY ApprovalLevel";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@LoggedUserID", SqlDbType.UniqueIdentifier).Value = loggedUserId;
            cmd.Parameters.Add("@ApprovalStatus", SqlDbType.Int).Value = approvalStatus;
            cmd.Parameters.Add("@Year", SqlDbType.Int).Value = year;

            var list = new List<SopDetail>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapSopDetail(reader));

            return (list.Count, list);
        }


        public async Task<bool> ApproveSop(Guid sopId, Guid approverId, string? comments, int nextApprovalLevel)
        {
            const string sql =
                @"
                  DECLARE @MaxApprovalLevel int = 0
                  SELECT @MaxApprovalLevel = MAX(WF.ApprovalLevel)
                  	FROM SopDetailsWorkFlowSetUp WF
                  WHERE  WF.IsDeleted = 0
                  
                  -- ApprovalLevel
                  UPDATE SopDetails
                  SET ApprovalLevel = @NextApprovalLevel,
                  	ApprovalStatus = IIF(@NextApprovalLevel = @MaxApprovalLevel, 3, ApprovalStatus),
                  	Modified = GETDATE(),
                  	ModifiedByID = @ApproverID
                  WHERE ID = @ID
                
                  -- Update EmployeeID
                  UPDATE SopDetails SET EmployeeID = @ApproverID WHERE ID = @ID           
                
                  -- Insert into SopDetailsApprovalHistory
                  INSERT INTO [SopDetailsApprovalHistory]
                        (ID, SopDetailsID, ApprovalLevel, ApprovalStatus,
                            Comments, ReferenceVersion, CreatedByID, Created)
                  SELECT
                        NEWID(), @ID, @NextApprovalLevel,
                        1,
                        @Comments,
                        SD.Version,
                        @ApproverID, GETDATE()
                  FROM [SopDetails] SD
                  WHERE SD.ID = @ID;";

            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            using var cmd = new SqlCommand(sql, conn, tx);

            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = sopId;
            cmd.Parameters.Add("@ApproverID", SqlDbType.UniqueIdentifier).Value = approverId;
            cmd.Parameters.Add("@NextApprovalLevel", SqlDbType.Int).Value = nextApprovalLevel;
            cmd.Parameters.Add("@Comments", SqlDbType.NVarChar).Value = (object?)comments ?? DBNull.Value;

            var rows = await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();

            _logger.LogInformation("SOP {SopId} approved by {ApproverId}", sopId, approverId);
            return rows > 0;
        }

        public async Task<bool> RejectSop(Guid sopId, Guid approverId, string? comments)
        {
            const string sql = @"
             UPDATE SopDetails
             SET ApprovalStatus = 2,
                 Modified = GETDATE(),
                 ModifiedByID = @ApproverID
             WHERE ID = @SopID
               AND IsDeleted = 0;
             
             INSERT INTO [SopDetailsApprovalHistory]
                  (ID, SopDetailsID, ApprovalLevel, ApprovalStatus, Comments, ReferenceVersion, CreatedByID, Created)
             SELECT 
                  NEWID(), SD.ID, SD.ApprovalLevel, 2, @Comments, SD.Version, @ApproverID, GETDATE()
             FROM SopDetails SD
             WHERE SD.ID = @SopID
               AND SD.IsDeleted = 0;";

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

        private static SopDetail MapSopDetail(SqlDataReader r)
         => new()
         {
             ID = r.GetGuid(r.GetOrdinal("ID")),
             SopTitle = r.IsDBNull(r.GetOrdinal("SopTitle"))
                    ? null
                    : r.GetString(r.GetOrdinal("SopTitle")),
             ExpirationDate = r.GetDateTime(r.GetOrdinal("ExpirationDate")),
             SopDocument = r.IsDBNull(r.GetOrdinal("SopDocument"))
                    ? null
                    : r.GetString(r.GetOrdinal("SopDocument")),

             SopDocumentVersion = r.GetInt32(r.GetOrdinal("SopDocumentVersion")),

            
             ApprovalLevel = r.GetInt32(r.GetOrdinal("ApprovalLevel")),
             NextApprovalLevel = r.GetInt32(r.GetOrdinal("NextApprovalLevel")),
             StageName = r.GetString(r.GetOrdinal("StageName")),
             NextStageName = r.GetString(r.GetOrdinal("NextStageName")),
             ApprovalStatus = ReadEnum<SopApprovalStatus>(r, "ApprovalStatus")
            
         };

        private static T? ReadEnum<T>(SqlDataReader r, string col) where T : struct, Enum
        {
            var o = r.GetOrdinal(col);
            if (r.IsDBNull(o)) return null;
            var intValue = r.GetInt32(o);     
            return (T)(object)intValue;       
        }

    }
}
