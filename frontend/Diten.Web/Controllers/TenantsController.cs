using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/[controller]")]
public sealed class TenantsController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View("~/Views/Platform/Tenants/Index.cshtml");
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("~/Views/Platform/Tenants/Create.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["TenantId"] = id;
        return View("~/Views/Platform/Tenants/Details.cshtml");
    }

    [HttpGet("/Platform/TenantSecurity")]
    public IActionResult Security()
    {
        return View("~/Views/Platform/Tenants/Security.cshtml");
    }
}
