using System.Security.Cryptography;
using System.Text;

namespace TecLogos.SOP.Common.Helpers
{
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLower();
        }

        public static bool Verify(string password, string hash)
            => Hash(password) == hash;

        public static string GenerateToken(int length = 64)
        {
            var bytes = RandomNumberGenerator.GetBytes(length);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }
}
