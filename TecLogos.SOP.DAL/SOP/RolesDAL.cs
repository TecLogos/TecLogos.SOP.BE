using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IRolesDAL
    {
        Task<List<Roles>> GetAll();
        Task<Roles?> GetById(Guid id);
        Task<Guid> Create(Roles role, Guid userId);
        Task<bool> Update(Roles role, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class RolesDAL : IRolesDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<RolesDAL> _logger;

        public RolesDAL(IConfiguration configuration, ILogger<RolesDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

       
        // GET ALL
       
        public async Task<List<Roles>> GetAll()
        {
            const string query = @"
                SELECT
                    ID,
                    Name,
                    Description,
                    Version,
                    IsActive,
                    IsDeleted,
                    Created,
                    CreatedByID,
                    Modified,
                    ModifiedByID,
                    Deleted,
                    DeletedByID
              FROM [Role]
                WHERE IsDeleted = 0
                ORDER BY Name;";

            var list = new List<Roles>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(MapRole(reader));

            return list;
        }

       
        // GET BY ID
       
        public async Task<Roles?> GetById(Guid id)
        {
            const string query = @"
                SELECT
                    ID,
                    Name,
                    Description,
                    Version,
                    IsActive,
                    IsDeleted,
                    Created,
                    CreatedByID,
                    Modified,
                    ModifiedByID,
                    Deleted,
                    DeletedByID
                FROM [Role]
                WHERE ID        = @ID
                AND IsDeleted   = 0;";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", id);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapRole(reader);

            return null;
        }

       
        // CREATE
       
        public async Task<Guid> Create(Roles role, Guid userId)
        {
            var newId = Guid.NewGuid();

            const string query = @"
                INSERT INTO [Role]
                (ID, Name, Description, CreatedByID)
                VALUES
                (@ID, @Name, @Description, @CreatedByID);";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", newId);
            command.Parameters.AddWithValue("@Name", role.Name ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Description", role.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CreatedByID", userId);

            await command.ExecuteNonQueryAsync();

            return newId;
        }

       
        // UPDATE
       
        public async Task<bool> Update(Roles role, Guid userId)
        {
            const string query = @"
                UPDATE [Role]
                SET Name         = @Name,
                    Description  = @Description,
                    Modified     = GETUTCDATE(),
                    ModifiedByID = @ModifiedByID,
                    Version      = Version + 1
                WHERE ID        = @ID
                AND IsDeleted   = 0;";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", role.ID);
            command.Parameters.AddWithValue("@Name", role.Name ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Description", role.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ModifiedByID", userId);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

       
        // SOFT DELETE
       
        public async Task<bool> Delete(Guid id, Guid userId)
        {
            const string query = @"
                UPDATE [Role]
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

       
        // PRIVATE MAPPER
       
        private static Roles MapRole(SqlDataReader r)
        {
            return new Roles
            {
                ID = r.GetGuid(r.GetOrdinal("ID")),
                Name = r.IsDBNull(r.GetOrdinal("Name")) ? null : r.GetString(r.GetOrdinal("Name")),
                Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
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