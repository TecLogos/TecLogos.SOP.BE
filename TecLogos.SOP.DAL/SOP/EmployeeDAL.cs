using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IEmployeeDAL
    {
        Task<(int totalCount, List<EmployeeList>)> GetAll(int pageNumber, int pageSize, string term);
        Task<Employee?> GetById(Guid id);
        Task<Guid> Create(Employee employee);
        Task<bool> Update(Employee employee);
        Task<bool> Delete(Guid id, Guid deletedByID);
        Task<bool> IsEmployeeEmailExists(string code);
    }

    public class EmployeeDAL : IEmployeeDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<EmployeeDAL> _logger;

        public EmployeeDAL(IConfiguration configuration, ILogger<EmployeeDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }


        // GET ALL (paged)

        public async Task<(int totalCount, List<EmployeeList>)> GetAll(int pageNumber, int pageSize, string term)
        {
            var query = @"
                SELECT COUNT(1)
                FROM [Employee] e WITH(NOLOCK)
                WHERE e.[IsDeleted] = 0
                    AND (@Term IS NULL OR LEN(@Term) = 0
                       
                         OR e.FirstName   LIKE '%' + @Term + '%'
                         OR e.MiddleName  LIKE '%' + @Term + '%'
                         OR e.LastName    LIKE '%' + @Term + '%'
                         OR e.Email       LIKE '%' + @Term + '%');

                SELECT e.ID, e.FirstName, e.MiddleName, e.LastName,
                       e.Email, e.MobileNumber, e.IsActive, e.Version
                      
                FROM [Employee] e WITH(NOLOCK)
                  
                WHERE e.[IsDeleted] = 0
                    AND (@Term IS NULL OR LEN(@Term) = 0
                      
                         OR e.FirstName   LIKE '%' + @Term + '%'
                         OR e.MiddleName  LIKE '%' + @Term + '%'
                         OR e.LastName    LIKE '%' + @Term + '%'
                         OR e.Email       LIKE '%' + @Term + '%')
                ORDER BY e.[Email]
                OFFSET (@PageNumber - 1) * @PageSize ROWS
                FETCH NEXT @PageSize ROWS ONLY;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@PageNumber", pageNumber);
            command.Parameters.AddWithValue("@PageSize", pageSize);
            command.Parameters.AddWithValue("@Term",
                string.IsNullOrWhiteSpace(term) ? DBNull.Value : term);

            int totalCount = 0;
            var employees = new List<EmployeeList>();

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                totalCount = reader.GetInt32(0);

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    employees.Add(new EmployeeList
                    {
                        ID = reader.GetGuid(reader.GetOrdinal("ID")),

                        FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                        MiddleName = reader.IsDBNull(reader.GetOrdinal("MiddleName")) ? null : reader.GetString(reader.GetOrdinal("MiddleName")),
                        LastName = reader.GetString(reader.GetOrdinal("LastName")),
                        Email = reader.GetString(reader.GetOrdinal("Email")),
                        MobileNumber = reader.GetString(reader.GetOrdinal("MobileNumber")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        Version = reader.GetInt32(reader.GetOrdinal("Version"))
                    });
                }
            }

            _logger.LogInformation("Employees fetched: {Count} / {Total}", employees.Count, totalCount);
            return (totalCount, employees);
        }


        // GET BY ID

        public async Task<Employee?> GetById(Guid id)
        {
            var query = @"
                SELECT
                    e.*,
                    er.RoleID
                   
                FROM [Employee] e WITH(NOLOCK)
              
                LEFT JOIN [EmployeeRole]    er WITH(NOLOCK) ON er.EmployeeID = e.ID AND er.IsDeleted = 0
               
                WHERE e.ID = @ID AND e.IsDeleted = 0;

                ";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", id);

            Employee? employee = null;

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                employee = MapEmployee(reader);

            if (employee == null)
                return null;

            return employee;
        }


        // CREATE

        public async Task<Guid> Create(Employee employee)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var employeeQuery = @"
                    INSERT INTO [Employee] (
                        ID, FirstName, MiddleName, LastName, Email,
                        MobileNumber,  CreatedByID
                    ) VALUES (
                        @ID, @FirstName, @MiddleName, @LastName, @Email,
                        @MobileNumber, @CreatedByID
                    );";

                using (var cmd = new SqlCommand(employeeQuery, connection, transaction))
                {
                    AddEmployeeParameters(cmd, employee);
                    await cmd.ExecuteNonQueryAsync();
                }

                var roleQuery = @"
                    INSERT INTO [EmployeeRole] (ID, EmployeeID, RoleID, CreatedByID)
                    VALUES (NEWID(), @EmployeeID, @RoleID, @CreatedByID);";

                using (var cmd = new SqlCommand(roleQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@EmployeeID", employee.ID);
                    cmd.Parameters.AddWithValue("@RoleID", employee.RoleID.HasValue ? employee.RoleID.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedByID", employee.CreatedByID);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return employee.ID;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }


        // UPDATE

        public async Task<bool> Update(Employee emp)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            var empQuery = @"
                    UPDATE Employee
                    SET
                         FirstName = @FirstName, MiddleName = @MiddleName,
                        LastName = @LastName, Email = @Email, MobileNumber = @MobileNumber,
                        ManagerID = COALESCE(@ManagerID, ManagerID),
                        IsActive = @IsActive,
                        Version = Version + 1, Modified = GETUTCDATE(), ModifiedByID = @ModifiedByID
                    WHERE ID = @ID AND Version = @Version AND IsDeleted = 0;";

            int rowsAffected;
            using (var cmd = new SqlCommand(empQuery, connection, transaction))
            {
                AddUpdateEmployeeParameters(cmd, emp);
                rowsAffected = await cmd.ExecuteNonQueryAsync();
            }

            if (rowsAffected == 0)
            {
                transaction.Rollback();
                return false;
            }


            // ── Role: get old value ──────────────────────────────────────
            Guid? oldRoleId = null;
            using (var cmd = new SqlCommand(
                "SELECT RoleID FROM [EmployeeRole] WHERE EmployeeID = @ID AND IsDeleted = 0",
                connection, transaction))
            {
                cmd.Parameters.AddWithValue("@ID", emp.ID);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    oldRoleId = (Guid)result;
            }

            if (emp.RoleID.HasValue)
            {
                var updateRoleQuery = @"
                        IF EXISTS (SELECT 1 FROM [EmployeeRole] WHERE EmployeeID = @ID AND IsDeleted = 0)
                            UPDATE [EmployeeRole]
                            SET RoleID = @RoleID, Version = Version + 1,
                                Modified = GETUTCDATE(), ModifiedByID = @ModifiedByID
                            WHERE EmployeeID = @ID AND IsDeleted = 0
                        ELSE
                            INSERT INTO [EmployeeRole] (ID, EmployeeID, RoleID, CreatedByID)
                            VALUES (NEWID(), @ID, @RoleID, @ModifiedByID)";

                using var cmd = new SqlCommand(updateRoleQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@ID", emp.ID);
                cmd.Parameters.AddWithValue("@RoleID", emp.RoleID.Value);
                cmd.Parameters.AddWithValue("@ModifiedByID", emp.ModifiedByID);
                await cmd.ExecuteNonQueryAsync();
            }

            if (emp.RoleID.HasValue && oldRoleId.HasValue && oldRoleId.Value != emp.RoleID.Value)
            {
                var overrideQuery = @"
                        INSERT INTO [RoleHistory] (
                            ID, EmployeeID, RoleID, OldRoleID,
                            ChangedOn, Remarks, CreatedByID
                        ) VALUES (
                            NEWID(), @ID, @NewRole, @OldRole,
                            GETUTCDATE(), 'Role changed during employee update', @ModifiedByID
                        )";

                using var cmd = new SqlCommand(overrideQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@ID", emp.ID);
                cmd.Parameters.AddWithValue("@NewRole", emp.RoleID.Value);
                cmd.Parameters.AddWithValue("@OldRole", oldRoleId.Value);
                cmd.Parameters.AddWithValue("@ModifiedByID", emp.ModifiedByID);
                await cmd.ExecuteNonQueryAsync();
            }


            transaction.Commit();
            return true;

        }


        // DELETE (soft)

        public async Task<bool> Delete(Guid id, Guid deletedByID)
        {
            var query = @"
                UPDATE [Employee]
                SET IsDeleted = 1, Deleted = GETUTCDATE(), DeletedByID = @DeletedByID
                WHERE ID = @ID;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", id);
            command.Parameters.AddWithValue("@DeletedByID", deletedByID);

            var affectedRows = await command.ExecuteNonQueryAsync();
            return affectedRows > 0;
        }


        // IS EMPLOYEE EMAIL EXISTS

        public async Task<bool> IsEmployeeEmailExists(string email)
        {
            var query = @"
                SELECT 1
                FROM [Employee] WITH(NOLOCK)
                WHERE [Email] = @Email AND [IsDeleted] = 0;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", email);

            var result = await command.ExecuteScalarAsync();
            return result != null;
        }


        // PRIVATE HELPERS


        private static Employee MapEmployee(SqlDataReader r)
        {
            T? Safe<T>(string col) where T : struct
            {
                int idx = r.GetOrdinal(col);
                return r.IsDBNull(idx) ? null : (T?)r.GetFieldValue<T>(idx);
            }
            string? SafeStr(string col)
            {
                int idx = r.GetOrdinal(col);
                return r.IsDBNull(idx) ? null : r.GetString(idx);
            }
            byte[]? SafeBytes(string col)
            {
                int idx = r.GetOrdinal(col);
                return r.IsDBNull(idx) ? null : (byte[])r.GetValue(idx);
            }

            return new Employee
            {
                ID = r.GetGuid(r.GetOrdinal("ID")),

                FirstName = r.GetString(r.GetOrdinal("FirstName")),
                MiddleName = SafeStr("MiddleName"),
                LastName = r.GetString(r.GetOrdinal("LastName")),
                Email = r.GetString(r.GetOrdinal("Email")),
                MobileNumber = r.GetString(r.GetOrdinal("MobileNumber")),

                RoleID = Safe<Guid>("RoleID"),

                IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
                IsDeleted = r.GetBoolean(r.GetOrdinal("IsDeleted")),
                Version = r.GetInt32(r.GetOrdinal("Version")),
                Created = Safe<DateTime>("Created"),
                CreatedByID = Safe<Guid>("CreatedByID"),
                Modified = Safe<DateTime>("Modified"),
                ModifiedByID = Safe<Guid>("ModifiedByID"),
                Deleted = Safe<DateTime>("Deleted"),
                DeletedByID = Safe<Guid>("DeletedByID")
            };
        }


        private static void AddEmployeeParameters(SqlCommand cmd, Employee e)
        {
            cmd.Parameters.AddWithValue("@ID", e.ID);
            cmd.Parameters.AddWithValue("@FirstName", e.FirstName);
            cmd.Parameters.AddWithValue("@MiddleName", e.MiddleName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LastName", e.LastName);
            cmd.Parameters.AddWithValue("@Email", e.Email);
            cmd.Parameters.AddWithValue("@MobileNumber", e.MobileNumber);
            cmd.Parameters.AddWithValue("@ManagerID", e.ManagerID.HasValue ? (object)e.ManagerID.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedByID", e.CreatedByID);
        }

        private static void AddUpdateEmployeeParameters(SqlCommand cmd, Employee e)
        {
            cmd.Parameters.AddWithValue("@ID", e.ID);

            cmd.Parameters.AddWithValue("@FirstName", e.FirstName);
            cmd.Parameters.AddWithValue("@MiddleName", e.MiddleName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LastName", e.LastName);
            cmd.Parameters.AddWithValue("@Email", e.Email);
            cmd.Parameters.AddWithValue("@MobileNumber", e.MobileNumber);
            cmd.Parameters.AddWithValue("@ManagerID", e.ManagerID.HasValue ? (object)e.ManagerID.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", e.IsActive);
            cmd.Parameters.AddWithValue("@Version", e.Version);
            cmd.Parameters.AddWithValue("@ModifiedByID", e.ModifiedByID);
        }
    }
}