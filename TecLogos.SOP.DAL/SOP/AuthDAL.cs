using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.DAL.SOP
{
    public interface IAuthDAL
    {
        Task<(Employee? employee, AuthManager? auth, string? roleName)> GetLoginDataAsync(string email);
        Task UpdateAuthOnLoginAsync(Guid employeeId, bool success, Guid modifiedBy);
        Task<string> CreateRefreshTokenAsync(Guid employeeId, string token, DateTime expiresAt, Guid createdBy);
        Task<(RefreshToken? token, Employee? employee, string? roleName)> GetRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token, string? replacedBy, string? ip, Guid modifiedBy);
        Task<AuthManager?> GetAuthByEmployeeIdAsync(Guid employeeId);
        Task UpdatePasswordAsync(Guid employeeId, string newHash, Guid modifiedBy);
        Task SetPasswordResetTokenAsync(Guid employeeId, string token, DateTime expires, Guid modifiedBy);
        Task<AuthManager?> GetByPasswordResetTokenAsync(string token);
        Task<OnboardingInvite?> GetOnboardingInviteAsync(string token);
        Task CompleteOnboardingAsync(Guid employeeId, string passwordHash, Guid inviteId, Guid modifiedBy);
        Task<string> CreateOnboardingInviteAsync(Guid employeeId, string token, DateTime expiry, Guid createdBy);
    }

    public class AuthDAL : IAuthDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<AuthDAL> _logger;

        public AuthDAL(IConfiguration configuration, ILogger<AuthDAL> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        private async Task<SqlConnection> OpenAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public async Task<(Employee? employee, AuthManager? auth, string? roleName)> GetLoginDataAsync(string email)
        {
            const string sql = @"
                SELECT e.ID, e.FirstName, e.MiddleName, e.LastName, e.Email,
                       e.MobileNumber, e.IsActive, e.IsDeleted,
                       am.PasswordHash, am.IsPasswordSet, am.IsFirstLogin,
                       am.IsLoginOnHold, am.FailedLoginAttempts, am.LastFailedLoginAttemptOn,
                       am.LastLoginDate, am.ID AS AuthID,
                       r.Name AS RoleName
                FROM Employee e WITH(NOLOCK)
                JOIN AuthManager am WITH(NOLOCK) ON am.EmployeeID = e.ID AND am.IsDeleted = 0
                LEFT JOIN EmployeeRole er WITH(NOLOCK) ON er.EmployeeID = e.ID AND er.IsDeleted = 0
                LEFT JOIN Role r WITH(NOLOCK) ON r.ID = er.RoleID AND r.IsDeleted = 0
                WHERE e.Email = @Email AND e.IsDeleted = 0;";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Email", email);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return (null, null, null);

            var employee = new Employee
            {
                ID = reader.GetGuid(reader.GetOrdinal("ID")),
                FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                MiddleName = reader.IsDBNull(reader.GetOrdinal("MiddleName")) ? null : reader.GetString(reader.GetOrdinal("MiddleName")),
                LastName = reader.GetString(reader.GetOrdinal("LastName")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                MobileNumber = reader.GetString(reader.GetOrdinal("MobileNumber")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted"))
            };

            var auth = new AuthManager
            {
                ID = reader.GetGuid(reader.GetOrdinal("AuthID")),
                EmployeeID = employee.ID,
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                IsPasswordSet = reader.GetBoolean(reader.GetOrdinal("IsPasswordSet")),
                IsFirstLogin = reader.GetBoolean(reader.GetOrdinal("IsFirstLogin")),
                IsLoginOnHold = reader.IsDBNull(reader.GetOrdinal("IsLoginOnHold")) ? null : reader.GetBoolean(reader.GetOrdinal("IsLoginOnHold")),
                FailedLoginAttempts = reader.GetInt32(reader.GetOrdinal("FailedLoginAttempts")),
                LastFailedLoginAttemptOn = reader.IsDBNull(reader.GetOrdinal("LastFailedLoginAttemptOn")) ? null : reader.GetDateTime(reader.GetOrdinal("LastFailedLoginAttemptOn")),
                LastLoginDate = reader.IsDBNull(reader.GetOrdinal("LastLoginDate")) ? null : reader.GetDateTime(reader.GetOrdinal("LastLoginDate"))
            };

            string? roleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString(reader.GetOrdinal("RoleName"));
            return (employee, auth, roleName);
        }

        public async Task UpdateAuthOnLoginAsync(Guid employeeId, bool success, Guid modifiedBy)
        {
            string sql = success
                ? @"UPDATE AuthManager SET FailedLoginAttempts = 0, LastLoginDate = GETUTCDATE(),
                        IsLoginOnHold = 0, Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy,
                        Version = Version + 1
                    WHERE EmployeeID = @EmpID AND IsDeleted = 0"
                : @"UPDATE AuthManager SET
                        FailedLoginAttempts = FailedLoginAttempts + 1,
                        LastFailedLoginAttemptOn = GETUTCDATE(),
                        IsLoginOnHold = CASE WHEN FailedLoginAttempts + 1 >= 5 THEN 1 ELSE IsLoginOnHold END,
                        Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy, Version = Version + 1
                    WHERE EmployeeID = @EmpID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<string> CreateRefreshTokenAsync(Guid employeeId, string token, DateTime expiresAt, Guid createdBy)
        {
            const string sql = @"
                INSERT INTO RefreshTokens (ID, EmployeeID, Token, ExpiresAt, CreatedByID)
                VALUES (NEWID(), @EmpID, @Token, @ExpiresAt, @CreatedBy)";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);
            cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
            await cmd.ExecuteNonQueryAsync();
            return token;
        }

        public async Task<(RefreshToken? token, Employee? employee, string? roleName)> GetRefreshTokenAsync(string token)
        {
            const string sql = @"
                SELECT rt.ID, rt.EmployeeID, rt.Token, rt.ExpiresAt, rt.RevokedAt,
                       rt.RevokedByIp, rt.ReplacedToken, rt.IsActive, rt.IsDeleted,
                       e.ID AS EID, e.FirstName, e.MiddleName, e.LastName,
                       e.Email, e.MobileNumber, e.IsActive AS EIsActive, e.IsDeleted AS EIsDeleted,
                       r.Name AS RoleName
                FROM RefreshTokens rt WITH(NOLOCK)
                JOIN Employee e WITH(NOLOCK) ON e.ID = rt.EmployeeID
                LEFT JOIN EmployeeRole er WITH(NOLOCK) ON er.EmployeeID = e.ID AND er.IsDeleted = 0
                LEFT JOIN Role r WITH(NOLOCK) ON r.ID = er.RoleID AND r.IsDeleted = 0
                WHERE rt.Token = @Token AND rt.IsDeleted = 0;";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return (null, null, null);

            var rt = new RefreshToken
            {
                ID = reader.GetGuid(reader.GetOrdinal("ID")),
                EmployeeID = reader.GetGuid(reader.GetOrdinal("EmployeeID")),
                Token = reader.GetString(reader.GetOrdinal("Token")),
                ExpiresAt = reader.GetDateTime(reader.GetOrdinal("ExpiresAt")),
                RevokedAt = reader.IsDBNull(reader.GetOrdinal("RevokedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("RevokedAt")),
                RevokedByIp = reader.IsDBNull(reader.GetOrdinal("RevokedByIp")) ? null : reader.GetString(reader.GetOrdinal("RevokedByIp")),
                ReplacedToken = reader.IsDBNull(reader.GetOrdinal("ReplacedToken")) ? null : reader.GetString(reader.GetOrdinal("ReplacedToken")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted"))
            };

            var emp = new Employee
            {
                ID = reader.GetGuid(reader.GetOrdinal("EID")),
                FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                MiddleName = reader.IsDBNull(reader.GetOrdinal("MiddleName")) ? null : reader.GetString(reader.GetOrdinal("MiddleName")),
                LastName = reader.GetString(reader.GetOrdinal("LastName")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                MobileNumber = reader.GetString(reader.GetOrdinal("MobileNumber")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("EIsActive")),
                IsDeleted = reader.GetBoolean(reader.GetOrdinal("EIsDeleted"))
            };

            string? roleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString(reader.GetOrdinal("RoleName"));
            return (rt, emp, roleName);
        }

        public async Task RevokeRefreshTokenAsync(string token, string? replacedBy, string? ip, Guid modifiedBy)
        {
            const string sql = @"
                UPDATE RefreshTokens
                SET RevokedAt = GETUTCDATE(), RevokedByIp = @IP,
                    ReplacedToken = @ReplacedBy, IsActive = 0,
                    Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy, Version = Version + 1
                WHERE Token = @Token AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@IP", ip ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ReplacedBy", replacedBy ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<AuthManager?> GetAuthByEmployeeIdAsync(Guid employeeId)
        {
            const string sql = @"
                SELECT ID, EmployeeID, PasswordHash, IsPasswordSet, IsFirstLogin,
                       IsLoginOnHold, FailedLoginAttempts, LastLoginDate,
                       LastFailedLoginAttemptOn, Version
                FROM AuthManager WITH(NOLOCK)
                WHERE EmployeeID = @EmpID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new AuthManager
            {
                ID = reader.GetGuid(reader.GetOrdinal("ID")),
                EmployeeID = reader.GetGuid(reader.GetOrdinal("EmployeeID")),
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                IsPasswordSet = reader.GetBoolean(reader.GetOrdinal("IsPasswordSet")),
                IsFirstLogin = reader.GetBoolean(reader.GetOrdinal("IsFirstLogin")),
                IsLoginOnHold = reader.IsDBNull(reader.GetOrdinal("IsLoginOnHold")) ? null : reader.GetBoolean(reader.GetOrdinal("IsLoginOnHold")),
                FailedLoginAttempts = reader.GetInt32(reader.GetOrdinal("FailedLoginAttempts")),
                LastLoginDate = reader.IsDBNull(reader.GetOrdinal("LastLoginDate")) ? null : reader.GetDateTime(reader.GetOrdinal("LastLoginDate")),
                LastFailedLoginAttemptOn = reader.IsDBNull(reader.GetOrdinal("LastFailedLoginAttemptOn")) ? null : reader.GetDateTime(reader.GetOrdinal("LastFailedLoginAttemptOn")),
                Version = reader.GetInt32(reader.GetOrdinal("Version"))
            };
        }

        public async Task UpdatePasswordAsync(Guid employeeId, string newHash, Guid modifiedBy)
        {
            const string sql = @"
                UPDATE AuthManager
                SET PasswordHash = @Hash, IsPasswordSet = 1, IsFirstLogin = 0,
                    PasswordResetToken = NULL, PasswordResetTokenExpires = NULL,
                    Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy, Version = Version + 1
                WHERE EmployeeID = @EmpID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Hash", newHash);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SetPasswordResetTokenAsync(Guid employeeId, string token, DateTime expires, Guid modifiedBy)
        {
            const string sql = @"
                UPDATE AuthManager
                SET PasswordResetToken = @Token, PasswordResetTokenExpires = @Expires,
                    Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy, Version = Version + 1
                WHERE EmployeeID = @EmpID AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@Expires", expires);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<AuthManager?> GetByPasswordResetTokenAsync(string token)
        {
            const string sql = @"
                SELECT ID, EmployeeID, PasswordHash, IsPasswordSet,
                       PasswordResetToken, PasswordResetTokenExpires, Version
                FROM AuthManager WITH(NOLOCK)
                WHERE PasswordResetToken = @Token AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new AuthManager
            {
                ID = reader.GetGuid(reader.GetOrdinal("ID")),
                EmployeeID = reader.GetGuid(reader.GetOrdinal("EmployeeID")),
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                IsPasswordSet = reader.GetBoolean(reader.GetOrdinal("IsPasswordSet")),
                PasswordResetToken = reader.IsDBNull(reader.GetOrdinal("PasswordResetToken")) ? null : reader.GetString(reader.GetOrdinal("PasswordResetToken")),
                PasswordResetTokenExpires = reader.IsDBNull(reader.GetOrdinal("PasswordResetTokenExpires")) ? null : reader.GetDateTime(reader.GetOrdinal("PasswordResetTokenExpires")),
                Version = reader.GetInt32(reader.GetOrdinal("Version"))
            };
        }

        public async Task<OnboardingInvite?> GetOnboardingInviteAsync(string token)
        {
            const string sql = @"
                SELECT ID, EmployeeID, Token, ExpiryDate, IsCompleted, Version
                FROM OnboardingInvites WITH(NOLOCK)
                WHERE Token = @Token AND IsDeleted = 0";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new OnboardingInvite
            {
                ID = reader.GetGuid(reader.GetOrdinal("ID")),
                EmployeeID = reader.GetGuid(reader.GetOrdinal("EmployeeID")),
                Token = reader.GetString(reader.GetOrdinal("Token")),
                ExpiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate")),
                IsCompleted = reader.GetBoolean(reader.GetOrdinal("IsCompleted")),
                Version = reader.GetInt32(reader.GetOrdinal("Version"))
            };
        }

        public async Task CompleteOnboardingAsync(Guid employeeId, string passwordHash, Guid inviteId, Guid modifiedBy)
        {
            using var conn = await OpenAsync();
            using var tx = conn.BeginTransaction();

            using (var cmd = new SqlCommand(@"
                UPDATE AuthManager
                SET PasswordHash = @Hash, IsPasswordSet = 1, IsFirstLogin = 0,
                    Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy, Version = Version + 1
                WHERE EmployeeID = @EmpID AND IsDeleted = 0", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Hash", passwordHash);
                cmd.Parameters.AddWithValue("@EmpID", employeeId);
                cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new SqlCommand(@"
                UPDATE OnboardingInvites
                SET IsCompleted = 1, Modified = GETUTCDATE(), ModifiedByID = @ModifiedBy, Version = Version + 1
                WHERE ID = @ID AND IsDeleted = 0", conn, tx))
            {
                cmd.Parameters.AddWithValue("@ID", inviteId);
                cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
                await cmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }

        public async Task<string> CreateOnboardingInviteAsync(Guid employeeId, string token, DateTime expiry, Guid createdBy)
        {
            const string sql = @"
                INSERT INTO OnboardingInvites (ID, EmployeeID, Token, ExpiryDate, IsCompleted, CreatedByID)
                VALUES (NEWID(), @EmpID, @Token, @Expiry, 0, @CreatedBy)";

            using var conn = await OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpID", employeeId);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@Expiry", expiry);
            cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
            await cmd.ExecuteNonQueryAsync();
            return token;
        }
    }
}
