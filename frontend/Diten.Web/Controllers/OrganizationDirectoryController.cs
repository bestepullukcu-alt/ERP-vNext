using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/OrganizationDirectory")]
public sealed class OrganizationDirectoryController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/OrganizationDirectory/Index.cshtml");
}
