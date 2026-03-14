using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IApprovalDAL
    {
        Task InsertApprovalHistoryAsync(SopDetailsApprovalHistory record);
        Task<List<SopDetailsApprovalHistory>> GetBySopIdAsync(Guid sopId);
        Task<bool> IsApproverForLevelAsync(Guid employeeId, int level);
    }

    public class ApprovalDAL : IApprovalDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<ApprovalDAL> _logger;

        public ApprovalDAL(IConfiguration configuration,
                           ILogger<ApprovalDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }


        public async Task InsertApprovalHistoryAsync(SopDetailsApprovalHistory record)
        {
             var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
            INSERT INTO SopDetailsApprovalHistory
                (SopDetailsID, ApprovalLevel, ApprovalStatus, ReferenceVersion, CreatedByID)
            VALUES
                (@SopDetailsID, @ApprovalLevel, @ApprovalStatus, @ReferenceVersion, @CreatedByID)", conn);

            cmd.Parameters.AddWithValue("@SopDetailsID", record.SopDetailsID);
            cmd.Parameters.AddWithValue("@ApprovalLevel", record.ApprovalLevel);
            cmd.Parameters.AddWithValue("@ApprovalStatus", record.ApprovalStatus);
            cmd.Parameters.AddWithValue("@ReferenceVersion", record.ReferenceVersion);
            cmd.Parameters.AddWithValue("@CreatedByID", record.CreatedByID);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<SopDetailsApprovalHistory>> GetBySopIdAsync(Guid sopId)
        {
            var list = new List<SopDetailsApprovalHistory>();
             var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
            SELECT * FROM SopDetailsApprovalHistory 
            WHERE SopDetailsID = @SopDetailsID AND IsDeleted = 0
            ORDER BY ApprovalLevel, Created", conn);
            cmd.Parameters.AddWithValue("@SopDetailsID", sopId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SopDetailsApprovalHistory
                {
                    ID = reader.GetGuid(reader.GetOrdinal("ID")),
                    SopDetailsID = reader.GetGuid(reader.GetOrdinal("SopDetailsID")),
                    ApprovalLevel = reader.GetInt32(reader.GetOrdinal("ApprovalLevel")),
                    ApprovalStatus = reader.GetInt32(reader.GetOrdinal("ApprovalStatus")),
                    ReferenceVersion = reader.GetInt32(reader.GetOrdinal("ReferenceVersion")),
                    CreatedByID = reader.GetGuid(reader.GetOrdinal("CreatedByID")),
                    Created = reader.GetDateTime(reader.GetOrdinal("Created"))
                });
            }
            return list;
        }

        public async Task<bool> IsApproverForLevelAsync(Guid employeeId, int level)
        {
             var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check if employee belongs to a group configured at this approval level
            var cmd = new SqlCommand(@"
            SELECT COUNT(1) FROM SopDetailsWorkFlowSetUp wf
            INNER JOIN EmployeeGroupDetail egd ON wf.EmployeeGroupID = egd.EmployeeGroupID
            WHERE egd.EmployeeID = @EmployeeID 
              AND wf.ApprovalLevel = @Level 
              AND wf.IsActive = 1 
              AND wf.IsDeleted = 0", conn);

            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Level", level);

            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }
    }

}
