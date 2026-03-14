using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TecLogos.SOP.WebApi.Controllers.SOP
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class BaseController : ControllerBase
    {
        protected Guid CurrentUserId
        {
            get
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst("sub");
                return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
            }
        }

        protected string IpAddress =>
            Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded)
                ? forwarded.ToString()
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
