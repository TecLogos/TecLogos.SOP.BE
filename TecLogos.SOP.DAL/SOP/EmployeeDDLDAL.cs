
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IEmployeeDDLDAL
    {
        Task<List<EmployeeDDL>> GetAll();
    }

    public class EmployeeDDLDAL : IEmployeeDDLDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<EmployeeDDLDAL> _logger;

        public EmployeeDDLDAL(IConfiguration configuration, ILogger<EmployeeDDLDAL> logger)
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

        public async Task<List<EmployeeDDL>> GetAll()
        {

            const string query = @"SELECT [ID], [FirstName], [LastName], [Email] FROM [Employee] WITH(NOLOCK) WHERE [IsDeleted] = 0 ORDER BY [FirstName], [LastName]";

            var list = new List<EmployeeDDL>();

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(query, conn);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new EmployeeDDL
                {
                    ID = GetGuid(reader, "ID"),
                    Email = GetString(reader, "Email") ?? "",
                    FirstName = GetString(reader, "FirstName"),
                    LastName = GetString(reader, "LastName")
                });
            }

            return list;
        }
        private static string? GetString(SqlDataReader r, string col)
            => r[col] == DBNull.Value ? null : r[col].ToString();

        private static Guid GetGuid(SqlDataReader r, string col)
            => r[col] == DBNull.Value ? Guid.Empty : (Guid)r[col];

    }
}
