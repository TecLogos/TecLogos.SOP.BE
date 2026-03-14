using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IEmployeeGroupDAL
    {
        Task<List<EmployeeGroup>> GetAll();
        Task<EmployeeGroup?> GetById(Guid id);
        Task<Guid> Create(EmployeeGroup group, Guid userId);
        Task<bool> Update(EmployeeGroup group, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class EmployeeGroupDAL : IEmployeeGroupDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<EmployeeGroupDAL> _logger;

        public EmployeeGroupDAL(IConfiguration configuration,
                                ILogger<EmployeeGroupDAL> logger)
        {
            _connectionString = configuration
                .GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        private SqlConnection CreateConnection()
            => new SqlConnection(_connectionString);

        
        // GET ALL
        
        public async Task<List<EmployeeGroup>> GetAll()
        {
            const string query = @"
                SELECT
                    ID,
                    Name,
                    Version,
                    IsActive,
                    IsDeleted,
                    Created,
                    CreatedByID,
                    Modified,
                    ModifiedByID,
                    Deleted,
                    DeletedByID
                FROM EmployeeGroup WITH (NOLOCK)
                WHERE IsDeleted = 0
                ORDER BY Name;";

            var list = new List<EmployeeGroup>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(MapEmployeeGroup(reader));

            _logger.LogInformation(
                "Retrieved {Count} active EmployeeGroups", list.Count);

            return list;
        }

        
        // GET BY ID
        
        public async Task<EmployeeGroup?> GetById(Guid id)
        {
            const string query = @"
                SELECT
                    ID,
                    Name,
                    Version,
                    IsActive,
                    IsDeleted,
                    Created,
                    CreatedByID,
                    Modified,
                    ModifiedByID,
                    Deleted,
                    DeletedByID
                FROM EmployeeGroup WITH (NOLOCK)
                WHERE ID = @ID
                AND IsDeleted = 0;";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", id);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapEmployeeGroup(reader);

            return null;
        }

        
        // CREATE
        
        public async Task<Guid> Create(EmployeeGroup group, Guid userId)
        {
            var newId = Guid.NewGuid();

            const string query = @"
                INSERT INTO EmployeeGroup (ID, Name, CreatedByID)
                VALUES (@ID, @Name, @CreatedByID);";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", newId);
            command.Parameters.AddWithValue("@Name", group.Name);
            command.Parameters.AddWithValue("@CreatedByID", userId);

            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("EmployeeGroup created with ID {ID}", newId);

            return newId;
        }

        
        // UPDATE
        
        public async Task<bool> Update(EmployeeGroup group, Guid userId)
        {
            const string query = @"
                UPDATE EmployeeGroup
                SET Name         = @Name,
                    Modified     = GETUTCDATE(),
                    ModifiedByID = @ModifiedByID,
                    Version      = Version + 1
                WHERE ID        = @ID
                AND IsDeleted   = 0;";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", group.ID);
            command.Parameters.AddWithValue("@Name", group.Name);
            command.Parameters.AddWithValue("@ModifiedByID", userId);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        
        // DELETE (soft)
        
        public async Task<bool> Delete(Guid id, Guid userId)
        {
            const string query = @"
                UPDATE EmployeeGroup
                SET IsDeleted   = 1,
                    IsActive    = 0,
                    Deleted     = GETUTCDATE(),
                    DeletedByID = @DeletedByID
                WHERE ID = @ID;";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", id);
            command.Parameters.AddWithValue("@DeletedByID", userId);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        
        // PRIVATE HELPER
        
        private static EmployeeGroup MapEmployeeGroup(SqlDataReader r)
        {
            return new EmployeeGroup
            {
                ID = r.GetGuid(r.GetOrdinal("ID")),
                Name = r.GetString(r.GetOrdinal("Name")),
                Version = r.GetInt32(r.GetOrdinal("Version")),
                IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
                IsDeleted = r.GetBoolean(r.GetOrdinal("IsDeleted")),
                Created = r.GetDateTime(r.GetOrdinal("Created")),
                CreatedByID = r.GetGuid(r.GetOrdinal("CreatedByID")),
                Modified = r.IsDBNull(r.GetOrdinal("Modified")) ? null : r.GetDateTime(r.GetOrdinal("Modified")),
                ModifiedByID = r.IsDBNull(r.GetOrdinal("ModifiedByID")) ? null : r.GetGuid(r.GetOrdinal("ModifiedByID")),
                Deleted = r.IsDBNull(r.GetOrdinal("Deleted")) ? null : r.GetDateTime(r.GetOrdinal("Deleted")),
                DeletedByID = r.IsDBNull(r.GetOrdinal("DeletedByID")) ? null : r.GetGuid(r.GetOrdinal("DeletedByID"))
            };
        }
    }
}