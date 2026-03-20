using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using TecLogos.SOP.DataModel.SOP;
using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.DAL.SOP
{
    public interface ISopDetailDAL
    {
        Task<(int totalCount, List<SopDetail>)> GetAllSops(int? approvalStatus, int? year);

        Task<(SopDetail? sop, int documentVersion)> GetSopById(Guid sopId);

        Task<Guid> CreateSop(SopDetail sop, string? commentText);
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
                    SD.ApprovalLevel,
                    WF.StageName,
                    ISNULL(WFN.StageName, '') NextStageName,
                    SD.ApprovalStatus,
                    SD.Version,
                   -- C.CommentText,
                    SD.IsActive,
                    SD.IsDeleted,
                    SD.Created,
                    SD.CreatedByID
                FROM  [SopDetails] SD WITH(NOLOCK)
                  --  LEFT JOIN Comment C WITH(NOLOCK) ON C.ReferenceID = SD.ID AND C.Version = SD.Version
                    LEFT JOIN SopDetailsWorkFlowSetUp WF WITH(NOLOCK) ON SD.ApprovalLevel = WF.ApprovalLevel AND WF.IsDeleted = 0
                    LEFT JOIN SopDetailsWorkFlowSetUp WFN WITH(NOLOCK) ON SD.ApprovalLevel + 1 = WFN.ApprovalLevel AND WFN.IsDeleted = 0
                WHERE SD.IsDeleted = 0
                  AND (@ApprovalStatus IS NULL OR SD.ApprovalStatus = @ApprovalStatus)
                  AND (@Year         IS NULL OR YEAR(SD.Created)   = @Year)
                ORDER BY SD.Created;
                
                IF EXISTS (SELECT 1 FROM SopDetails WITH(NOLOCK) WHERE CONVERT(DATE, ExpirationDate) < CONVERT(DATE, GETDATE()))
                BEGIN
                	UPDATE SopDetails
                	SET ApprovalStatus = 4
                	WHERE CONVERT(DATE, ExpirationDate) < CONVERT(DATE, GETDATE()) 
                END";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ApprovalStatus", SqlDbType.Int).Value = (object?)approvalStatus ?? DBNull.Value;
            cmd.Parameters.Add("@Year", SqlDbType.Int).Value = (object?)year ?? DBNull.Value;

            var list = new List<SopDetail>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                SopDetail item = MapSopDetail(reader);
                item.StageName = reader.IsDBNull(reader.GetOrdinal("StageName"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("StageName"));
                item.NextStageName = reader.IsDBNull(reader.GetOrdinal("NextStageName"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("NextStageName"));
                list.Add(item);
            }

            return (list.Count, list);
        }
        public async Task<(SopDetail? sop, int documentVersion)> GetSopById(Guid sopId)
        {
            const string sql =
                @"
                SELECT
                    SD.ID,
                    SD.SopTitle,
                    SD.ExpirationDate,
                    SD.SopDocument,
                    SD.SopDocumentVersion,
                    SD.ApprovalLevel,
                    SD.ApprovalStatus,
                    C.CommentText,
                    SD.Version,
                    SD.IsActive,
                    SD.IsDeleted,
                    SD.Created,
                    SD.CreatedByID
                FROM [SopDetails] SD
                    LEFT JOIN Comment C ON C.ReferenceID = SD.ID AND C.Version = SD.Version
                WHERE SD.ID = @SopID
                  AND SD.IsDeleted = 0
                              
                SELECT SDAH.ID, SDAH.ApprovalStatus, WF.StageName, SDAH.Comments, SDAH.[ReferenceVersion] Version, SDAH.Created, EMP.FirstName + ' ' + EMP.LastName [CreatedBy]
                FROM [SopDetailsApprovalHistory] SDAH WITH(NOLOCK)
                	INNER JOIN Employee EMP WITH(NOLOCK) ON EMP.ID = SDAH.CreatedByID
                	INNER JOIN SopDetailsWorkFlowSetUp WF WITH(NOLOCK) ON WF.ApprovalLevel = SDAH.ApprovalLevel
                WHERE SDAH.SopDetailsID = @SopID
                ORDER BY SDAH.Created, SDAH.ApprovalLevel

                 SELECT SH.SopTitle, SH.SopDocument, ISNULL(C.CommentText,'') CommentText, SH.Version, EMP.FirstName + ' ' + EMP.LastName [CreatedBy], SH.Created
                 FROM [SopDetailsHistory] SH WITH(NOLOCK) 
	                LEFT JOIN Comment C WITH(NOLOCK) ON C.ReferenceID = SH.SopDetailsID AND C.Version = SH.Version
	                INNER JOIN Employee EMP WITH(NOLOCK) ON EMP.ID = SH.CreatedByID
                 WHERE SH.SopDetailsID = @SopID
                 ORDER BY SH.Created, SH.Version
                ";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@SopID", SqlDbType.UniqueIdentifier).Value = sopId;

            using var reader = await cmd.ExecuteReaderAsync();

            SopDetail sop = null;
            int docVersion = 0;


            if (await reader.ReadAsync())
            {
                 sop = MapSopDetail(reader);

                docVersion = reader.GetInt32(reader.GetOrdinal("SopDocumentVersion"));

            }

            
           await reader.NextResultAsync();
            sop.SopApprovalHistoryList = [];
            while (await reader.ReadAsync())
            {
                sop.SopApprovalHistoryList.Add(MapApprovalHistory(reader));
            }

            
            await reader.NextResultAsync();
            sop.SopDetailHistoryList = [];
            while (await reader.ReadAsync())
            {
                sop.SopDetailHistoryList.Add(MapSopDetailHistory(reader));
            }

            return (sop, docVersion);
        }

        public async Task<Guid> CreateSop(SopDetail sop, string? commentText)
        {
            const string sql = @"
                INSERT INTO [SopDetails]
                (ID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                 ApprovalLevel, ApprovalStatus, EmployeeID, CreatedByID, Created)
                VALUES
                (@ID, @SopTitle, @ExpirationDate, @SopDocument, 1,
                 0, 0, @EmployeeID, @CreatedByID, GETDATE());
               
                INSERT INTO [SopDetailsHistory]
                (SopDetailsID, SopTitle, ExpirationDate, SopDocument,
                 SopDocumentVersion, ApprovalLevel, ApprovalStatus,
                 EmployeeID, CreatedByID, Created)
                VALUES
                (@ID, @SopTitle, @ExpirationDate, @SopDocument,
                 1, 0, 0, @EmployeeID, @CreatedByID, GETDATE());
               
               INSERT INTO [Comment]
               (
                   ID, ReferenceID, CommentText, Version, CreatedByID, Created
               )
               VALUES
               (
                   NEWID(), @ID, @CommentText, 1, @CreatedByID, GETDATE()
               )
                ";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = sop.ID;
            cmd.Parameters.Add("@SopTitle", SqlDbType.NVarChar).Value = (object?)sop.SopTitle ?? DBNull.Value;
            cmd.Parameters.Add("@ExpirationDate", SqlDbType.DateTime2).Value = (object?)sop.ExpirationDate ?? DBNull.Value;
            cmd.Parameters.Add("@SopDocument", SqlDbType.NVarChar).Value = (object?)sop.SopDocument ?? DBNull.Value;
            cmd.Parameters.Add("@EmployeeID", SqlDbType.UniqueIdentifier).Value = sop.CreatedByID;
            cmd.Parameters.Add("@CreatedByID", SqlDbType.UniqueIdentifier).Value = sop.CreatedByID;
            cmd.Parameters.Add("@CommentText", SqlDbType.NVarChar).Value = (object?)commentText ?? DBNull.Value;

            await cmd.ExecuteNonQueryAsync();
            return sop.ID;
        }

        public async Task<bool> UpdateSop(Guid sopId, DateTime? expirationDate, string? remark, Guid modifiedById, bool isFileUploaded, string? filePath)
        {
            const string sql =
                @"
                 UPDATE [SopDetails]
                 SET    ExpirationDate = @ExpirationDate,
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
                 WHERE ID = @SopID;
                 
                 INSERT INTO [SopDetailsHistory]
                 (
                     SopDetailsID, SopTitle, ExpirationDate, SopDocument, SopDocumentVersion,
                     ApprovalLevel, ApprovalStatus, EmployeeID, CreatedByID, Created
                 )
                 SELECT 
                     SD.ID, SD.SopTitle, SD.ExpirationDate, SD.SopDocument, SD.SopDocumentVersion,
                     SD.ApprovalLevel, SD.ApprovalStatus, SD.EmployeeID, CreatedByID, Created
                 FROM SopDetails SD
                 WHERE SD.ID = @SopID;

                 DECLARE @CommentVersion INT = (SELECT TOP 1 Version FROM SopDetails WHERE ID = @SopID)
                
                 INSERT INTO[Comment]
                 (
                     ID, ReferenceID, CommentText, Version, CreatedByID, Created
                 )
                 VALUES
                 (
                     NEWID(), @SopID, @CommentText, @CommentVersion, @ModifiedByID, GETDATE()
                 );

                 ";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@SopID", sopId);
            cmd.Parameters.AddWithValue("@ExpirationDate", (object?)expirationDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CommentText", (object?)remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedByID", modifiedById);
            cmd.Parameters.AddWithValue("@IsFileUploaded", isFileUploaded);
            cmd.Parameters.AddWithValue("@FilePath", (object?)filePath ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync();

            if (rows > 0)
            {
                _logger.LogInformation("SOP updated with history. ID={SopId} By={User}", sopId, modifiedById);
            }

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


        private static SopDetail MapSopDetail(SqlDataReader r)
        {
            return new SopDetail
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
                //CommentText = r.IsDBNull(r.GetOrdinal("CommentText"))
                //    ? null
                //    : r.GetString(r.GetOrdinal("CommentText")),

                ApprovalLevel = r.GetInt32(r.GetOrdinal("ApprovalLevel")),
                

                ApprovalStatus = ReadEnum<SopApprovalStatus>(r, "ApprovalStatus"),

                Version = r.GetInt32(r.GetOrdinal("Version")),
                IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
                IsDeleted = r.GetBoolean(r.GetOrdinal("IsDeleted")),
                Created = r.GetDateTime(r.GetOrdinal("Created")),
                CreatedByID = r.GetGuid(r.GetOrdinal("CreatedByID"))
            };
        }

        private static SopApprovalHistory MapApprovalHistory(SqlDataReader r)
        {
            return new SopApprovalHistory
            {
                ApprovalStatus = r.GetInt32(r.GetOrdinal("ApprovalStatus")),

                StageName = r.IsDBNull(r.GetOrdinal("StageName"))
                    ? null
                    : r.GetString(r.GetOrdinal("StageName")),

                Comments = r.IsDBNull(r.GetOrdinal("Comments"))
                    ? null
                    : r.GetString(r.GetOrdinal("Comments")),

                Version = r.GetInt32(r.GetOrdinal("Version")),

                Created = r.GetDateTime(r.GetOrdinal("Created")),

                CreatedBy = r.IsDBNull(r.GetOrdinal("CreatedBy"))
                    ? null
                    : r.GetString(r.GetOrdinal("CreatedBy")),
            };
        }

        private static SopDetailHistory MapSopDetailHistory(SqlDataReader r)
        {
            return new SopDetailHistory
            {
                SopTitle = r.IsDBNull(r.GetOrdinal("SopTitle"))
                    ? null
                    : r.GetString(r.GetOrdinal("SopTitle")),

                SopDocument = r.IsDBNull(r.GetOrdinal("SopDocument"))
                    ? null
                    : r.GetString(r.GetOrdinal("SopDocument")),

                CommentText = r.IsDBNull(r.GetOrdinal("CommentText"))
                    ? null
                    : r.GetString(r.GetOrdinal("CommentText")),

                Version = r.IsDBNull(r.GetOrdinal("Version"))
                    ? 0   
                    : r.GetInt32(r.GetOrdinal("Version")),

                Created = r.IsDBNull(r.GetOrdinal("Created"))
                    ? DateTime.MinValue
                    : r.GetDateTime(r.GetOrdinal("Created")),

                CreatedBy = r.IsDBNull(r.GetOrdinal("CreatedBy"))
                    ? null
                    : r.GetString(r.GetOrdinal("CreatedBy")),
            };
        }

        private static T? ReadEnum<T>(SqlDataReader r, string col) where T : struct, Enum
        {
            var o = r.GetOrdinal(col);
            if (r.IsDBNull(o)) return null;
            var intValue = r.GetInt32(o);        
            return (T)(object)intValue;          
        }


    }
}