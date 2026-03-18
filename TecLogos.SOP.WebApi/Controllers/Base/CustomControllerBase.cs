using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TecLogos.SOP.WebApi.Controllers.Base
{
    public class CustomControllerBase : ControllerBase
    {
        /// <summary>Returns the authenticated employee's ID from the JWT NameIdentifier claim.</summary>
        protected Guid CurrentUserId
        {
            get
            {
                var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
            }
        }

    }
}