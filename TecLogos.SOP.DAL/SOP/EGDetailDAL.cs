using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IEGDetailDAL
    {
        Task<List<EGDetail>> GetAll();
        Task<EGDetail?> GetById(Guid id);
        Task<Guid> Create(EGDetail group, Guid userId);
        Task<bool> Update(EGDetail group, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class EGDetailDAL : IEGDetailDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<EGDetailDAL> _logger;

        public EGDetailDAL(IConfiguration configuration,
                           ILogger<EGDetailDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }
        private async Task<SqlConnection> GetOpenConnectionAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        // GET ALL WITH EMPLOYEE INFO
        public async Task<List<EGDetail>> GetAll()
        {
            const string query = @"
             SELECT 
                 D.ID,
                 D.EmployeeGroupID,
                 G.Name AS GroupName,
                 D.EmployeeID,
                 (E.FirstName + ' ' + E.LastName) AS EmployeeName,
                 E.Email
             FROM EmployeeGroupDetail D WITH(NOLOCK)
             INNER JOIN EmployeeGroup G WITH(NOLOCK) ON G.ID = D.EmployeeGroupID
             INNER JOIN Employee E WITH(NOLOCK) ON E.ID = D.EmployeeID
             WHERE D.IsDeleted = 0
             ORDER BY G.Name, EmployeeName";

            var list = new List<EGDetail>();

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new EGDetail
                {
                    ID = GetGuid(reader, "ID"),
                    EmployeeGroupID = GetGuid(reader, "EmployeeGroupID"),
                    GroupName = GetString(reader, "GroupName"),
                    EmployeeID = GetGuid(reader, "EmployeeID"),
                    EmployeeName = GetString(reader, "EmployeeName"),
                    Email = GetString(reader, "Email")
                });
            }

            return list;
        }

        // GET BY ID
        public async Task<EGDetail?> GetById(Guid id)
        {
            const string sql = @"
                SELECT 
                    D.ID,
                    D.EmployeeGroupID,
                    G.Name AS GroupName,
                    D.EmployeeID,
                    (E.FirstName + ' ' + E.LastName) AS EmployeeName,
                    E.Email
                FROM EmployeeGroupDetail D WITH(NOLOCK)
                INNER JOIN EmployeeGroup G WITH(NOLOCK) ON G.ID = D.EmployeeGroupID
                INNER JOIN Employee E WITH(NOLOCK) ON E.ID = D.EmployeeID
                WHERE D.ID = @ID AND D.IsDeleted = 0";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new EGDetail
            {
                ID = GetGuid(reader, "ID"),
                EmployeeGroupID = GetGuid(reader, "EmployeeGroupID"),
                GroupName = GetString(reader, "GroupName"),
                EmployeeID = GetGuid(reader, "EmployeeID"),
                EmployeeName = GetString(reader, "EmployeeName"),
                Email = GetString(reader, "Email")
            };
        }

        // CREATE (Assign employee to group)
        public async Task<Guid> Create(EGDetail model, Guid userId)
        {
            var id = Guid.NewGuid();

            const string sql = @"
                 INSERT INTO EmployeeGroupDetail
                 (ID, EmployeeGroupID, EmployeeID, CreatedByID)
                 VALUES
                 (@ID, @EmployeeGroupID, @EmployeeID, @User)";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@ID", id);
            cmd.Parameters.AddWithValue("@EmployeeGroupID", model.EmployeeGroupID);
            cmd.Parameters.AddWithValue("@EmployeeID", model.EmployeeID);
            cmd.Parameters.AddWithValue("@User", userId);

            await cmd.ExecuteNonQueryAsync();
            return id;
        }

        // UPDATE (Change employee group mapping)
        public async Task<bool> Update(EGDetail model, Guid userId)
        {
            const string sql = @"
                UPDATE EmployeeGroupDetail
                SET EmployeeGroupID = @EmployeeGroupID,
                    EmployeeID = @EmployeeID,
                    Modified = GETUTCDATE(),
                    ModifiedByID = @User,
                    Version = Version + 1
                WHERE ID = @ID
                AND IsDeleted = 0";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@ID", model.ID);
            cmd.Parameters.AddWithValue("@EmployeeGroupID", model.EmployeeGroupID);
            cmd.Parameters.AddWithValue("@EmployeeID", model.EmployeeID);
            cmd.Parameters.AddWithValue("@User", userId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }


        // SOFT DELETE (Remove employee from group)
        public async Task<bool> Delete(Guid id, Guid userId)
        {
            const string sql = @"
                UPDATE EmployeeGroupDetail
                SET IsDeleted = 1,
                    IsActive = 0,
                    Deleted = GETUTCDATE(),
                    DeletedByID = @User
                WHERE ID = @ID";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@ID", id);
            cmd.Parameters.AddWithValue("@User", userId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        #region PRIVATE METHODS
        private static string? GetString(SqlDataReader r, string col)
            => r[col] == DBNull.Value ? null : r[col].ToString();

        private static Guid GetGuid(SqlDataReader r, string col)
            => r[col] == DBNull.Value ? Guid.Empty : (Guid)r[col];
        #endregion
    }

}