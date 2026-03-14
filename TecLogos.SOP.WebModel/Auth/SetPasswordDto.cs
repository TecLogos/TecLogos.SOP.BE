using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecLogos.SOP.WebModel.Auth
{
    public class SetPasswordDto
    {
        public string Token { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ValidateInviteResponse
    {
        public bool IsValid { get; set; }
    }
}
