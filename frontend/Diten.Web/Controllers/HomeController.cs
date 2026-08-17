using System.Diagnostics;
using Diten.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// FE-A-core (MOD-0018-FU9): makes the configured exception page (/Home/Error) reachable and serves
// friendly 401/403/404 status pages. [AllowAnonymous] so the global ShellAccessFilter never blocks an
// error/status page — these must render even for an unauthenticated caller. The views are
// self-contained (Layout = null), so they never depend on the frozen _Layout shell.
[AllowAnonymous]
public sealed class HomeController : Controller
{
    [Route("Home/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    [Route("Home/Status/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int code)
    {
        // Preserve the original status code on the re-executed response.
        Response.StatusCode = code;
        return View("Status", StatusPageViewModel.For(code, HttpContext.TraceIdentifier));
    }
}
