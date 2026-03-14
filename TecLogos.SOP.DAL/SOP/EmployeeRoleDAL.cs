using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IEmployeeRoleDAL
    {
        Task<(int totalCount, List<EmployeeRoles>)> GetAll(Guid? employeeId, int? year, int? month);
        Task<(int totalCount, List<RoleHistory>)> TrackRoleHistory(Guid? employeeId, int? year, int? month);
    }

    public class EmployeeRoleDAL : IEmployeeRoleDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<EmployeeRoleDAL> _logger;

        public EmployeeRoleDAL(IConfiguration configuration, ILogger<EmployeeRoleDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

       
        // GET CURRENT EMPLOYEE ROLES
       
        public async Task<(int totalCount, List<EmployeeRoles>)> GetAll(Guid? employeeId, int? year, int? month)
        {
            const string query = @"
                SELECT
                    er.ID,
                    e.ID   AS EmployeeID,
                    r.ID   AS RoleID,
                    e.Email,
                    e.FirstName,
                    e.MiddleName,
                    e.LastName,
                    r.Name AS RoleName,
                    r.Description
                FROM dbo.EmployeeRole er WITH(NOLOCK)
                JOIN dbo.Employee e WITH(NOLOCK) ON e.ID = er.EmployeeID
                JOIN dbo.Role    r WITH(NOLOCK) ON r.ID = er.RoleID
                WHERE e.IsDeleted = 0
                AND (@EmployeeID IS NULL OR er.EmployeeID = @EmployeeID)
                ORDER BY e.Email;";

            var list = new List<EmployeeRoles>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@EmployeeID",
                employeeId.HasValue ? employeeId.Value : DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(MapEmployeeRoles(reader));

            return (list.Count, list);
        }

       
        // TRACK ROLE HISTORY
       
        public async Task<(int totalCount, List<RoleHistory>)> TrackRoleHistory(Guid? employeeId, int? year, int? month)
        {
            var range = BuildDateRange(year, month);

            const string query = @"
                SELECT
                    ro.ID,
                    e.ID          AS EmployeeID,
                    ro.RoleID,
                    e.Email,
                    e.FirstName,
                    e.MiddleName,
                    e.LastName,
                    newRole.Name  AS RoleName,
                    oldRole.Name  AS OldRoleName,
                    ro.OldRoleID,
                    ro.ChangedOn,
                    ro.Remarks
                FROM dbo.RoleHistory ro WITH(NOLOCK)
                JOIN dbo.Employee  e       WITH(NOLOCK) ON e.ID       = ro.EmployeeID
                JOIN dbo.Role     newRole WITH(NOLOCK) ON newRole.ID = ro.RoleID
                JOIN dbo.Role     oldRole WITH(NOLOCK) ON oldRole.ID = ro.OldRoleID
                WHERE e.IsDeleted = 0
                AND (@EmployeeID IS NULL OR ro.EmployeeID = @EmployeeID)
                AND (@Start      IS NULL OR ro.ChangedOn >= @Start)
                AND (@End        IS NULL OR ro.ChangedOn <  @End)
                ORDER BY e.Email;";

            var list = new List<RoleHistory>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@EmployeeID",
                employeeId.HasValue ? employeeId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Start",
                range.start.HasValue ? range.start.Value : DBNull.Value);
            command.Parameters.AddWithValue("@End",
                range.end.HasValue ? range.end.Value : DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(MapRoleHistory(reader));

            return (list.Count, list);
        }

       
        // DATE FILTER HELPER
       
        private static (DateTime? start, DateTime? end) BuildDateRange(int? year, int? month)
        {
            if (year == null)
                return (null, null);

            if (month == null)
            {
                var start = new DateTime(year.Value, 1, 1);
                return (start, start.AddYears(1));
            }

            var s = new DateTime(year.Value, month.Value, 1);
            return (s, s.AddMonths(1));
        }

       
        // PRIVATE MAPPERS
       
        private static EmployeeRoles MapEmployeeRoles(SqlDataReader r)
        {
            return new EmployeeRoles
            {
                ID = r.GetGuid(r.GetOrdinal("ID")),
                EmployeeID = r.GetGuid(r.GetOrdinal("EmployeeID")),
                RoleID = r.GetGuid(r.GetOrdinal("RoleID")),
                Email = r.GetString(r.GetOrdinal("Email")),
                FirstName = r.GetString(r.GetOrdinal("FirstName")),
                MiddleName = r.IsDBNull(r.GetOrdinal("MiddleName")) ? null : r.GetString(r.GetOrdinal("MiddleName")),
                LastName = r.GetString(r.GetOrdinal("LastName")),
                RoleName = r.GetString(r.GetOrdinal("RoleName")),
                Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description"))
            };
        }

        private static RoleHistory MapRoleHistory(SqlDataReader r)
        {
            return new RoleHistory
            {
                ID = r.GetGuid(r.GetOrdinal("ID")),
                EmployeeID = r.GetGuid(r.GetOrdinal("EmployeeID")),
                RoleID = r.GetGuid(r.GetOrdinal("RoleID")),
                Email = r.GetString(r.GetOrdinal("Email")),
                FirstName = r.GetString(r.GetOrdinal("FirstName")),
                MiddleName = r.IsDBNull(r.GetOrdinal("MiddleName")) ? null : r.GetString(r.GetOrdinal("MiddleName")),
                LastName = r.GetString(r.GetOrdinal("LastName")),
                RoleName = r.GetString(r.GetOrdinal("RoleName")),
                OldRoleName = r.GetString(r.GetOrdinal("OldRoleName")),
                OldRoleID = r.GetGuid(r.GetOrdinal("OldRoleID")),
                ChangedOn = r.GetDateTime(r.GetOrdinal("ChangedOn")),
                Remarks = r.IsDBNull(r.GetOrdinal("Remarks")) ? null : r.GetString(r.GetOrdinal("Remarks"))
            };
        }
    }
}