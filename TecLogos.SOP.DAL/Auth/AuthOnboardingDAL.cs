using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TecLogos.SOP.DAL.Auth
{
    public interface IAuthOnboardingDAL
    {
        Task<string> CreateOnboardingInvite(Guid employeeId, Guid createdBy);
        Task<(Guid? EmployeeId, string? Error)> ValidateInviteToken(string token);
        Task CompleteOnboarding(Guid employeeId, string token, string passwordHash);
        Task MarkInviteUsed(string token);
        Task<string> GetEmployeeEmail(Guid employeeId);
        Task<(string Email, string Code)> GetEmployeeByInviteToken(string token);
    }

    public class AuthOnboardingDAL : IAuthOnboardingDAL
    {
        private readonly string _connectionString;
        private readonly ILogger<AuthOnboardingDAL> _logger;

        public AuthOnboardingDAL(IConfiguration configuration, ILogger<AuthOnboardingDAL> logger)
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

        // CREATE INVITE TOKEN
        public async Task<string> CreateOnboardingInvite(Guid employeeId, Guid createdBy)
        {
            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            const string sql = 
                @"
                  INSERT INTO OnboardingInvites
                  (ID, EmployeeID, Token, ExpiryDate, CreatedByID)
                  VALUES
                  (@ID, @EmployeeID, @Token, DATEADD(HOUR,48,GETUTCDATE()), @CreatedByID)
                 ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@ID", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@CreatedByID", createdBy);

            await cmd.ExecuteNonQueryAsync();
            return token;
        }

        // VALIDATE TOKEN
        public async Task<(Guid? EmployeeId, string? Error)> ValidateInviteToken(string token)
        {
            const string sql =
                @"
                  SELECT EmployeeID, ExpiryDate, IsCompleted
                  FROM OnboardingInvites
                  WHERE Token=@Token
                ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return (null, "Invalid token");

            var isUsed = GetBool(reader, "IsUsed");
            var expiry = GetDateTime(reader, "ExpiryDate");

            if (isUsed)
                return (null, "Invite already used");

            if (expiry < DateTime.Now)
                return (null, "Invite expired");

            return (GetGuid(reader, "EmployeeID"), null);
        }

        // Complete On-Boarding
        public async Task CompleteOnboarding(Guid employeeId, string token, string passwordHash)
        {
            using var conn = await GetOpenConnectionAsync();
            using var tx = conn.BeginTransaction();

                // MARK INVITE USED
                var markCmd = new SqlCommand(
                    @"
                      UPDATE OnboardingInvites 
                      SET IsUsed = 1, Modified = GETUTCDATE()
                      WHERE Token = @Token
                     ", conn, tx);

                markCmd.Parameters.AddWithValue("@Token", token);
                var rows = await markCmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    throw new ArgumentException("Invite already used or invalid");

                // UPSERT AUTH MANAGER
                var upsertCmd = new SqlCommand(
                    @"
                      IF EXISTS (SELECT 1 FROM AuthManager WHERE EmployeeID = @EmployeeID)
                      BEGIN
                          UPDATE AuthManager
                          SET PasswordHash=@PasswordHash,
                              IsPasswordSet=1,
                              IsFirstLogin=0,
                              IsActive=1,
                              Modified=GETUTCDATE(),
                              ModifiedByID=@EmployeeID
                          WHERE EmployeeID=@EmployeeID
                      END
                      ELSE
                      BEGIN
                          INSERT INTO AuthManager
                          (ID,EmployeeID,PasswordHash,IsPasswordSet,IsFirstLogin,IsActive,Created,CreatedByID)
                          VALUES
                          (NEWID(),@EmployeeID,@PasswordHash,1,0,1,GETUTCDATE(),@EmployeeID)
                      END
                     ", conn, tx);

                upsertCmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                upsertCmd.Parameters.AddWithValue("@PasswordHash", passwordHash);

                await upsertCmd.ExecuteNonQueryAsync();

                tx.Commit();
        }

        // MARK INVITE USED
        public async Task MarkInviteUsed(string token)
        {
            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(
                "UPDATE OnboardingInvites SET IsUsed=1 WHERE Token=@Token", conn);

            cmd.Parameters.AddWithValue("@Token", token);
            await cmd.ExecuteNonQueryAsync();
        }

        // GET EMPLOYEE EMAIL
        public async Task<string> GetEmployeeEmail(Guid employeeId)
        {
            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(
                "SELECT Email FROM Employee WHERE ID=@ID", conn);

            cmd.Parameters.AddWithValue("@ID", employeeId);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "";
        }

        public async Task<(string Email, string Code)> GetEmployeeByInviteToken(string token)
        {
            const string sql =
            @"
              SELECT EmployeeID, ExpiryDate, IsCompleted
              FROM OnboardingInvites
              WHERE Token=@Token
             ";

            using var conn = await GetOpenConnectionAsync();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new ArgumentException("Invalid onboarding token");

            return (
                GetString(reader, "Email")!,
                GetString(reader, "Code")!
            );
        }

        #region Helper Methods
        private static string? GetString(SqlDataReader r, string col)
            => r[col] == DBNull.Value ? null : r[col].ToString();

        private static Guid GetGuid(SqlDataReader r, string col)
            => r[col] == DBNull.Value ? Guid.Empty : (Guid)r[col];

        private static DateTime GetDateTime(SqlDataReader r, string col)
            => Convert.ToDateTime(r[col]);

        private static bool GetBool(SqlDataReader r, string col)
            => Convert.ToBoolean(r[col]);
        #endregion

    }
}
