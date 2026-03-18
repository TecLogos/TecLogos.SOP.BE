using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using TecLogos.SOP.DataModel.Auth;

namespace TecLogos.SOP.DAL.Auth
{
    public interface IAuthDAL
    {
        Task<AuthEmployeeEntity?> GetEmployeeByEmail(string Email);
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

            // Resolve access from EmployeeGroupDetail + workflow setup (group-based installs).
            // This avoids depending on JWT Role claims, which are not always present.
            var access = await GetAccessFlags(conn, employeeId);
            profile.IsAdmin = access.IsAdmin;
            profile.IsInitiator = access.IsInitiator;
            profile.IsSupervisor = access.IsSupervisor;
            profile.IsApprover = access.IsApprover;
            profile.ResolvedRole = ResolveRole(access);

            return profile;
        }

        private static string ResolveRole((bool IsAdmin, bool IsInitiator, bool IsSupervisor, bool IsApprover) access)
        {
            if (access.IsAdmin) return "Admin";
            if (access.IsApprover) return "Approver";
            if (access.IsSupervisor) return "Supervisor";
            if (access.IsInitiator) return "Initiator";
            // Safe fallback
            return "Initiator";
        }

        private async Task<(bool IsAdmin, bool IsInitiator, bool IsSupervisor, bool IsApprover)> GetAccessFlags(SqlConnection conn, Guid employeeId)
        {
            // Workflow levels:
            // 0 = Not Started  (Admin group)
            // 1 = In Progress  (Initiator group)
            // 2 = Submitted    (Supervisor, manager-based)
            // 3..5 Approvals   (Approver groups)
            var adminGroupId = await GetWorkflowEmployeeGroupId(conn, approvalLevel: 0);
            var initGroupId = await GetWorkflowEmployeeGroupId(conn, approvalLevel: 1);
            var approverGroupIds = await GetWorkflowEmployeeGroupIds(conn, minApprovalLevelInclusive: 3);

            var isAdmin = adminGroupId.HasValue && await IsInEmployeeGroup(conn, employeeId, adminGroupId.Value);
            var isInitiator = initGroupId.HasValue && await IsInEmployeeGroup(conn, employeeId, initGroupId.Value);
            var isApprover = approverGroupIds.Count > 0 && await IsInAnyEmployeeGroup(conn, employeeId, approverGroupIds);
            var isSupervisor = await HasDirectReports(conn, employeeId);

            return (isAdmin, isInitiator, isSupervisor, isApprover);
        }

        private static async Task<Guid?> GetWorkflowEmployeeGroupId(SqlConnection conn, int approvalLevel)
        {
            const string sql = @"
SELECT TOP 1 EmployeeGroupID
FROM SopDetailsWorkFlowSetUp WITH(NOLOCK)
WHERE IsDeleted = 0
  AND IsActive  = 1
  AND IsSupervisor = 0
  AND ApprovalLevel = @Level
  AND EmployeeGroupID IS NOT NULL
ORDER BY Created DESC;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Level", approvalLevel);
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return null;
            return (Guid)result;
        }

        private static async Task<List<Guid>> GetWorkflowEmployeeGroupIds(SqlConnection conn, int minApprovalLevelInclusive)
        {
            const string sql = @"
SELECT DISTINCT EmployeeGroupID
FROM SopDetailsWorkFlowSetUp WITH(NOLOCK)
WHERE IsDeleted = 0
  AND IsActive  = 1
  AND IsSupervisor = 0
  AND ApprovalLevel >= @MinLevel
  AND EmployeeGroupID IS NOT NULL;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MinLevel", minApprovalLevelInclusive);
            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<Guid>();
            while (await reader.ReadAsync())
            {
                if (reader[0] != DBNull.Value) list.Add((Guid)reader[0]);
            }
            reader.Close();
            return list;
        }

        private static async Task<bool> IsInEmployeeGroup(SqlConnection conn, Guid employeeId, Guid employeeGroupId)
        {
            const string sql = @"
SELECT TOP 1 1
FROM EmployeeGroupDetail WITH(NOLOCK)
WHERE IsDeleted = 0
  AND IsActive  = 1
  AND EmployeeID = @EmployeeID
  AND EmployeeGroupID = @GroupID;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@GroupID", employeeGroupId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }

        private static async Task<bool> IsInAnyEmployeeGroup(SqlConnection conn, Guid employeeId, List<Guid> groupIds)
        {
            if (groupIds.Count == 0) return false;

            // Build an IN list safely with parameters
            var paramNames = new List<string>();
            using var cmd = new SqlCommand();
            cmd.Connection = conn;
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);

            for (var i = 0; i < groupIds.Count; i++)
            {
                var p = "@G" + i;
                paramNames.Add(p);
                cmd.Parameters.AddWithValue(p, groupIds[i]);
            }

            cmd.CommandText = $@"
SELECT TOP 1 1
FROM EmployeeGroupDetail WITH(NOLOCK)
WHERE IsDeleted = 0
  AND IsActive  = 1
  AND EmployeeID = @EmployeeID
  AND EmployeeGroupID IN ({string.Join(",", paramNames)});";

            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }

        private static async Task<bool> HasDirectReports(SqlConnection conn, Guid managerId)
        {
            const string sql = @"
SELECT TOP 1 1
FROM Employee WITH(NOLOCK)
WHERE IsDeleted = 0
  AND IsActive  = 1
  AND ManagerID = @ManagerID;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ManagerID", managerId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null;
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
