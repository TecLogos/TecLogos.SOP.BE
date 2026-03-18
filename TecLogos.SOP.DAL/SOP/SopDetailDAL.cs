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
        Task<(int totalCount, List<SopDetail>)> GetAllSops(int? approvalStatus, int? year);

        Task<(SopDetail? sop, int documentVersion)> GetSopById(Guid sopId);

        Task<Guid> CreateSop(SopDetail sop);
        Task<bool> UpdateSop(Guid sopId, DateTime? expirationDate, string? remark, Guid modifiedById, bool isFileUploaded, string? filePath);
        Task<bool> Delete(Guid sopId, Guid deletedById);
   
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

        public async Task<(int totalCount, List<SopDetail>)> GetAllSops(int? approvalStatus, int? year)
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
        public async Task<(SopDetail? sop, int documentVersion)> GetSopById(Guid sopId)
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
                              FROM [SopDetails] SD
                              WHERE SD.ID = @SopID
                                AND SD.IsDeleted = 0;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@SopID", SqlDbType.UniqueIdentifier).Value = sopId;

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var sop = MapSopDetail(reader, levelColumn: "ApprovalLevel");

                // 👇 This is your version
                var docVersion = reader.GetInt32(reader.GetOrdinal("SopDocumentVersion"));

                return (sop, docVersion);
            }

            return (null, 0);
        }
        public async Task<Guid> CreateSop(SopDetail sop)
        {
            const string sql = @"
                INSERT INTO [SopDetails]
                    (ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                     Remark, ApprovalLevel, ApprovalStatus, EmployeeID, CreatedByID, Created)
                VALUES
                    (@ID, @SopTitle, @ExpirationDate, @SopDocument, 1,
                     @Remark, 0, 0, @EmployeeID, @CreatedByID, GETDATE());

                INSERT INTO [SopDetailsHistory]
                    (SopDetailsID, SopTitle, ExpirationDate, SopDocument,
                     SopDocumentVersion, Remark, ApprovalLevel, ApprovalStatus,
                     EmployeeID, CreatedByID, Created)
                VALUES
                    (@ID, @SopTitle, @ExpirationDate, @SopDocument,
                     1, @Remark, 0, 0, @EmployeeID, @CreatedByID, GETDATE());";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = sop.ID;
            cmd.Parameters.Add("@SopTitle", SqlDbType.NVarChar).Value = (object?)sop.SopTitle ?? DBNull.Value;
            cmd.Parameters.Add("@ExpirationDate", SqlDbType.DateTime2).Value = (object?)sop.ExpirationDate ?? DBNull.Value;
            cmd.Parameters.Add("@SopDocument", SqlDbType.NVarChar).Value = (object?)sop.SopDocument ?? DBNull.Value;
            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar).Value = (object?)sop.Remark ?? DBNull.Value;
            cmd.Parameters.Add("@EmployeeID", SqlDbType.UniqueIdentifier).Value = sop.CreatedByID;
            cmd.Parameters.Add("@CreatedByID", SqlDbType.UniqueIdentifier).Value = sop.CreatedByID;

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("SOP created. ID={SopId} By={EmpId}", sop.ID, sop.CreatedByID);
            return sop.ID;
        }

        public async Task<bool> UpdateSop(Guid sopId, DateTime? expirationDate, string? remark, Guid modifiedById, bool isFileUploaded, string? filePath)
        {
            const string sql =
                @"
        UPDATE [SopDetails]
        SET    ExpirationDate = @ExpirationDate,
               Remark         = @Remark,
               Modified       = GETDATE(),
               ModifiedByID   = @ModifiedByID,
               Version        = Version + 1,

               SopDocumentVersion =
                    CASE 
                        WHEN @IsFileUploaded = 1 THEN SopDocumentVersion + 1
                        ELSE SopDocumentVersion
                    END,

               SopDocument =
                    CASE 
                        WHEN @IsFileUploaded = 1 THEN @FilePath
                        ELSE SopDocument
                    END

        WHERE ID = @SopID
          AND IsDeleted = 0;
        ";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@SopID", sopId);
            cmd.Parameters.AddWithValue("@ExpirationDate", (object?)expirationDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remark", (object?)remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedByID", modifiedById);
            cmd.Parameters.AddWithValue("@IsFileUploaded", isFileUploaded);
            cmd.Parameters.AddWithValue("@FilePath", (object?)filePath ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        public async Task<bool> Delete(Guid sopId, Guid deletedById)
        {
            const string sql = @"
                UPDATE [SopDetails]
                SET    IsDeleted   = 1,
                       IsActive    = 0,
                       Deleted     = GETDATE(),
                       DeletedByID = @DeletedByID
                WHERE  ID        = @SopID
                  AND  IsDeleted = 0;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@SopID", SqlDbType.UniqueIdentifier).Value = sopId;
            cmd.Parameters.Add("@DeletedByID", SqlDbType.UniqueIdentifier).Value = deletedById;

            var rows = await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("SOP soft-deleted. ID={SopId}", sopId);
            return rows > 0;
        }


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

        private static string? ReadString(SqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetString(o); }
        private static DateTime? ReadDateTime(SqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetDateTime(o); }
        private static int? ReadInt(SqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetInt32(o); }

       private static T? ReadEnum<T>(SqlDataReader r, string col) where T : struct, Enum
        {
            var o = r.GetOrdinal(col);
            if (r.IsDBNull(o)) return null;
            var intValue = r.GetInt32(o);        
            return (T)(object)intValue;          
        }


    }
}