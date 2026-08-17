using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

public class WorkCenterController : Controller
{
    [HttpGet]
    public IActionResult Index(string? tab)
    {
        var normalizedTab = tab?.ToLowerInvariant() switch
        {
            "all" => "all",
            "inbox" => "inbox",
            _ => "inbox"
        };

        ViewBag.ActiveMenu = "workcenter";
        ViewBag.InitialTab = normalizedTab;

        return View();
    }

    [HttpGet]
    public IActionResult Task(string id, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && !Url.IsLocalUrl(returnUrl))
            returnUrl = null;

        ViewBag.TaskId = id ?? string.Empty;
        ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "WorkCenter");
        ViewBag.ActiveMenu = "workcenter";

        return View();
    }

    [HttpGet]
    public IActionResult Meeting(string id, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && !Url.IsLocalUrl(returnUrl))
            returnUrl = null;

        ViewBag.MeetingId = id ?? string.Empty;
        ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "WorkCenter");
        ViewBag.ActiveMenu = "workcenter";

        return View();
    }

    [HttpGet]
    public IActionResult DevScenarios()
    {
        ViewBag.ActiveMenu = "workcenter";
        return View();
    }
}
