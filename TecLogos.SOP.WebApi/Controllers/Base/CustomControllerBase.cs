using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace TecLogos.SOP.WebApi.Controllers.Base
{
    public class CustomControllerBase: ControllerBase
    {
        protected Guid CurrentUserId
        {
            get
            {
                var id = User?.FindFirstValue(ClaimTypes.NameIdentifier);
                return id is null ? Guid.Empty : Guid.Parse(id);
            }
        }

    }
}
