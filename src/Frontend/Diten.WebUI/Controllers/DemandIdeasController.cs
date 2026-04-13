using Microsoft.AspNetCore.Mvc;
using Diten.WebUI.Models.DemandIdeas;

namespace Diten.WebUI.Controllers;

public sealed class DemandIdeasController : Controller
{
    private readonly IConfiguration _configuration;

    public DemandIdeasController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Dashboard() => View();

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Detail(string id) => RedirectToAction(nameof(Capture), new { id });

    [HttpGet]
    public IActionResult Capture(string? id = null)
    {
        ViewData["CaptureLayoutVersion"] = "enterprise-api-2026-02";
        // Empty = browser calls same-origin /api/v1/... (API is hosted in WebUI). Set PublicApiUrl to use a different API host.
        var publicApi = _configuration["ApiSettings:PublicApiUrl"]?.TrimEnd('/') ?? "";
        return View(new CaptureShellViewModel { ApiBaseUrl = publicApi, InitialRecordId = id });
    }

    [HttpGet]
    public IActionResult Decomposition() => RedirectToAction("Index", "DecompositionTreeBuilder");

    [HttpGet]
    public IActionResult WbsValidation() => Placeholder("WBS Validation");
    [HttpGet]
    public IActionResult ReadinessAssessment() => Placeholder("Readiness Assessment");
    [HttpGet]
    public IActionResult TransferToPpm() => Placeholder("Transfer to PPM");
    [HttpGet]
    public IActionResult Stakeholders() => Placeholder("Stakeholders");
    [HttpGet]
    public IActionResult PortfolioLinks() => Placeholder("Portfolio Links");
    [HttpGet]
    public IActionResult TemplatesRules() => Placeholder("Templates & Rules");

    private IActionResult Placeholder(string title)
    {
        ViewData["ModuleTitle"] = title;
        return View("ModulePlaceholder");
    }
}
