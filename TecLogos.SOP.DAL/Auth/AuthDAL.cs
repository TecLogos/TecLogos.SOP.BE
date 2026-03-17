using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using TecLogos.SOP.DataModel.Auth;

namespace TecLogos.SOP.DAL.Auth
{
    public interface IAuthDAL
    {
        Task<AuthEmployeeEntity?> GetEmployeeByEmail(string Email);
        Task<List<Role>> GetEmployeeRoles(Guid employeeId);
        Task UpdateLastLogin(Guid employeeId);
        Task IncrementFailedLoginAttempts(string Email);
        Task<RefreshToken?> GetRefreshToken(string token);
        Task SaveRefreshToken(Guid employeeId, string refreshToken, string ipAddress, int expirationDays);
        Task RevokeRefreshToken(Guid tokenId, string ipAddress);
        Task<int> RevokeAllTokensForEmployee(Guid employeeId, string ipAddress);
        Task<AuthEmployee?> GetUserProfile(Guid employeeId);

    }

    public class AuthDAL : IAuthDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<AuthDAL> _logger;

        public AuthDAL(IConfiguration configuration,
                              ILogger<AuthDAL> logger)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        private async Task<SqlConnection> GetOpenConnectionAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        // GET EMPLOYEE
        public async Task<AuthEmployeeEntity?> GetEmployeeByEmail(string Email)
        {
            const string sql =
                @"
        SELECT TOP 1
            e.ID,
            e.Email,
            e.IsActive,
            am.PasswordHash,
            am.FailedLoginAttempts,
            am.LastFailedLoginAttemptOn,
            am.LastLoginDate
        FROM Employee e WITH(NOLOCK)
        INNER JOIN AuthManager am WITH(NOLOCK) ON am.EmployeeID = e.ID
        WHERE e.Email = @Login
  AND e.IsDeleted = 0
  AND e.IsActive = 1
  AND am.IsDeleted = 0
  AND am.IsActive = 1
        ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Login", Email);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!reader.Read())
                return null;

            return new AuthEmployeeEntity
            {
                ID = GetGuid(reader, "ID"),
                Email = GetString(reader, "Email"),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                PasswordHash = GetString(reader, "PasswordHash"),
                FailedLoginAttempts = GetInt(reader, "FailedLoginAttempts"),
                LastFailedLoginAttemptOn = GetDate(reader, "LastFailedLoginAttemptOn"),
                LastLoginDate = GetDate(reader, "LastLoginDate")
            };
        }

        public async Task<List<Role>> GetEmployeeRoles(Guid employeeId)
        {
            // ── Step 1: Try explicit EmployeeRole assignment (Admin has this) ─────────
            const string explicitRoleSql = @"
        SELECT r.ID AS RoleID, r.Name AS RoleName
        FROM   [EmployeeRole] er WITH(NOLOCK)
        INNER JOIN [Role] r WITH(NOLOCK) ON r.ID = er.RoleID
        WHERE  er.EmployeeID = @EmployeeID
          AND  er.IsDeleted  = 0
          AND  er.IsActive   = 1
          AND  r.IsDeleted   = 0
          AND  r.IsActive    = 1;";

            var roles = new List<Role>();

            using var conn = await GetOpenConnectionAsync();
            using var cmd1 = new SqlCommand(explicitRoleSql, conn);
            cmd1.Parameters.Add("@EmployeeID", SqlDbType.UniqueIdentifier).Value = employeeId;

            using var reader1 = await cmd1.ExecuteReaderAsync();
            while (await reader1.ReadAsync())
            {
                roles.Add(new Role
                {
                    RoleID = (Guid)reader1["RoleID"],
                    RoleName = reader1["RoleName"]?.ToString() ?? string.Empty
                });
            }
            reader1.Close();

            // If explicit role found (e.g. Admin), return immediately
            if (roles.Count > 0)
                return roles;

            // ── Step 2: Derive role from EmployeeGroup membership ────────────────────
            //
            //  Join chain:
            //    EmployeeGroupDetail (EGD) → EmployeeGroup (EG) → SopDetailsWorkFlowSetUp (WF)
            //
            //  Mapping logic (matches seed data + workflow setup):
            //    WF.IsSupervisor = 1            → "Supervisor"
            //    WF.ApprovalLevel = 1           → "Initiator"   (In Progress stage group)
            //    WF.ApprovalLevel IN (3, 4, 5)  → "Approver"    (L1/L2/L3 approver groups)
            //    WF.ApprovalLevel = 0           → "Admin"        (Admin group fallback)
            //
            const string derivedRoleSql = @"
        SELECT DISTINCT
            CASE
                WHEN WF.IsSupervisor = 1        THEN 'Supervisor'
                WHEN WF.ApprovalLevel = 1        THEN 'Initiator'
                WHEN WF.ApprovalLevel IN (3,4,5) THEN 'Approver'
                WHEN WF.ApprovalLevel = 0        THEN 'Admin'
                ELSE                                  'Initiator'
            END AS DerivedRole
        FROM   [EmployeeGroupDetail] EGD WITH(NOLOCK)
        INNER JOIN [EmployeeGroup]            EG  WITH(NOLOCK)
               ON  EG.ID        = EGD.EmployeeGroupID
              AND  EG.IsDeleted  = 0
        INNER JOIN [SopDetailsWorkFlowSetUp]  WF  WITH(NOLOCK)
               ON  WF.EmployeeGroupID = EG.ID
              AND  WF.IsDeleted       = 0
        WHERE  EGD.EmployeeID = @EmployeeID
          AND  EGD.IsDeleted  = 0
          AND  EGD.IsActive   = 1;";

            using var cmd2 = new SqlCommand(derivedRoleSql, conn);
            cmd2.Parameters.Add("@EmployeeID", SqlDbType.UniqueIdentifier).Value = employeeId;

            using var reader2 = await cmd2.ExecuteReaderAsync();

            // Take the first derived role (employees should be in one functional group)
            if (await reader2.ReadAsync())
            {
                var derivedRoleName = reader2["DerivedRole"]?.ToString() ?? "Initiator";
                roles.Add(new Role
                {
                    RoleID = Guid.Empty,         // no real Role row — derived from group
                    RoleName = derivedRoleName
                });
            }
            reader2.Close();

            // Final fallback — should never happen, but prevents null role edge case
            if (roles.Count == 0)
            {
                roles.Add(new Role { RoleID = Guid.Empty, RoleName = "Initiator" });
            }

            return roles;
        }

        // LOGIN TRACKING
        public async Task UpdateLastLogin(Guid employeeId)
        {
            const string sql = 
                @"
                  UPDATE AuthManager
                  SET LastLoginDate = GETUTCDATE(),
                      FailedLoginAttempts = 0,
                      LastFailedLoginAttemptOn = NULL,
                      Modified = GETUTCDATE()
                  WHERE EmployeeID = @EmployeeID
                ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task IncrementFailedLoginAttempts(string email)
        {
            const string sql =
            @"
    UPDATE AuthManager
    SET FailedLoginAttempts = ISNULL(FailedLoginAttempts,0) + 1,
        LastFailedLoginAttemptOn = GETUTCDATE(),
        Modified = GETUTCDATE()
    WHERE EmployeeID IN
    (
        SELECT ID
        FROM Employee
        WHERE Email = @Email
          AND IsDeleted = 0
    )
    ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = email;

            await cmd.ExecuteNonQueryAsync();
        }

        // REFRESH TOKEN

        public async Task<RefreshToken?> GetRefreshToken(string token)
        {
            const string sql =
                @"
        SELECT *
        FROM RefreshTokens WITH(NOLOCK)
        WHERE Token = @Token
AND IsDeleted = 0
AND IsActive = 1
AND RevokedAt IS NULL
AND ExpiresAt > GETUTCDATE()
        ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.Read()) return null;

            return new RefreshToken
            {
                ID = GetGuid(reader, "ID"),
                EmployeeID = GetGuid(reader, "EmployeeID"),
                Token = GetString(reader, "Token"),
                ExpiresAt = Convert.ToDateTime(reader["ExpiresAt"])
            };
        }

        public async Task SaveRefreshToken(Guid employeeId, string refreshToken, string ipAddress, int expirationDays)
        {
            const string sql =
            @"
    INSERT INTO RefreshTokens
    (
        ID,
        EmployeeID,
        Token,
        ExpiresAt,
        RevokedByIp,
        IsActive,
        IsDeleted,
        Created,
        CreatedByID
    )
    VALUES
    (
        @ID,
        @EmployeeID,
        @Token,
        @ExpiresAt,
        @Ip,
        1,
        0,
        GETUTCDATE(),
        @CreatedByID
    )
    ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            cmd.Parameters.Add("@EmployeeID", SqlDbType.UniqueIdentifier).Value = employeeId;
            cmd.Parameters.Add("@Token", SqlDbType.NVarChar, 255).Value = refreshToken;
            cmd.Parameters.Add("@ExpiresAt", SqlDbType.DateTime2).Value = DateTime.Now.AddDays(expirationDays);
            cmd.Parameters.Add("@Ip", SqlDbType.NVarChar, 45).Value = ipAddress;
            cmd.Parameters.Add("@CreatedByID", SqlDbType.UniqueIdentifier).Value = employeeId;

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RevokeRefreshToken(Guid tokenId, string ipAddress)
        {
            const string sql =
            @"
    UPDATE RefreshTokens
    SET RevokedAt = GETUTCDATE(),
        RevokedByIp = @Ip,
        IsActive = 0,
        Modified = GETUTCDATE()
    WHERE ID = @ID
      AND IsDeleted = 0
    ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = tokenId;
            cmd.Parameters.Add("@Ip", SqlDbType.NVarChar, 45).Value = ipAddress;

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> RevokeAllTokensForEmployee(Guid employeeId, string ipAddress)
        {
            const string sql =
                @"
                 UPDATE RefreshTokens
                 SET RevokedAt = GETUTCDATE(),
                     RevokedByIp = @Ip,
                     IsActive = 0,
                     Modified = GETUTCDATE()
                 WHERE EmployeeID = @EmployeeID
                   AND RevokedAt IS NULL
                ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Ip", ipAddress);

            return await cmd.ExecuteNonQueryAsync();
        }
        // PROFILE
        public async Task<AuthEmployee?> GetUserProfile(Guid employeeId)
        {
            const string sql = @"
        SELECT
            e.ID,
            e.Email,
            e.MobileNumber,
            e.FirstName,
            e.LastName
        FROM [Employee] e WITH(NOLOCK)
        WHERE e.ID        = @ID
          AND e.IsDeleted = 0;";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var profile = new AuthEmployee
            {
                ID = GetGuid(reader, "ID"),
                FullName = $"{GetString(reader, "FirstName")} {GetString(reader, "LastName")}",
                Email = GetString(reader, "Email") ?? string.Empty,
                MobileNumber = GetString(reader, "MobileNumber") ?? string.Empty,
            };
            reader.Close();

            // Roles are populated by AuthBAL.GetUserProfile → _authDAL.GetEmployeeRoles
            // (AuthBAL already calls GetEmployeeRoles and sets profile.Roles)
            return profile;
        }
        #region Private Helpers
        private static string? GetString(SqlDataReader reader, string column)
        {
            return reader[column] == DBNull.Value ? null : reader[column].ToString();
        }

        private static Guid GetGuid(SqlDataReader reader, string column)
        {
            return reader[column] == DBNull.Value ? Guid.Empty : (Guid)reader[column];
        }

        private static int GetInt(SqlDataReader reader, string column)
        {
            return reader[column] == DBNull.Value ? 0 : Convert.ToInt32(reader[column]);
        }

        private static DateTime? GetDate(SqlDataReader reader, string column)
        {
            return reader[column] == DBNull.Value ? null : Convert.ToDateTime(reader[column]);
        }
        #endregion
    }
}
