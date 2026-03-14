using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecLogos.SOP.BAL.Auth
{
    public interface IPasswordHasherBAL
    {
        string Hash(string password);
        bool Verify(string password, string hash);
    }

    public class PasswordHasherBAL : IPasswordHasherBAL
    {
        public string Hash(string password)
           => BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string hash)
            => BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
