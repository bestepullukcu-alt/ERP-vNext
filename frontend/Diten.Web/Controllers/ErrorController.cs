using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0029-FU04C — friendly Not Authorized surface for full-page / navigation requests that hit a backend
// 403 (authenticated but lacking permission). [AllowAnonymous] so the page itself can never loop back through an
// authz filter; the view is self-contained (Layout = null) like the existing /Home/Status pages. Action endpoints
// keep returning the 403 Response<T> envelope — this only changes how a NAVIGATION 403 is presented to the user.
[AllowAnonymous]
[Route("Error")]
public sealed class ErrorController : Controller
{
    [HttpGet("NotAuthorized")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult NotAuthorized([FromQuery] int? code)
    {
        // 401 (unauthenticated) recovers via login; 403 (authenticated, no permission) shows Not Authorized.
        Response.StatusCode = code == 401 ? 401 : 403;
        return View("~/Views/Shared/NotAuthorized.cshtml", Response.StatusCode);
    }
}
