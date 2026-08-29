using Diten.Web.Models.ManagementGovernance.Dws;
using Diten.Web.Services.ManagementGovernance.Dws;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("management-governance/delivery-execution/structures")]
public sealed class DwsStructuresFrontendController : Controller
{
    private readonly DwsFrontendGatewayProxy _proxy;

    public DwsStructuresFrontendController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _proxy = new DwsFrontendGatewayProxy(
            httpClient,
            configuration,
            loggerFactory.CreateLogger<DwsFrontendGatewayProxy>());
    }

    [HttpGet("")]
    public IActionResult Index() => View(
        "~/Views/ManagementGovernance/Dws/Index.cshtml",
        new DwsFrontendIndexViewModel(
            _proxy.IsEnabled,
            _proxy.HasPermission(User, DwsFrontendPermissions.Read),
            _proxy.HasPermission(User, DwsFrontendPermissions.Validate),
            _proxy.HasPermission(User, DwsFrontendPermissions.Compare)));

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        _proxy.GetAsync(HttpContext, id.ToString("D"), DwsFrontendPermissions.Read, token);

    [HttpGet("api/{id:guid}/tree")]
    public Task<IActionResult> Tree(Guid id, [FromQuery] int? revisionNumber, CancellationToken token) =>
        _proxy.GetAsync(HttpContext, Path(id, "tree", ("revisionNumber", revisionNumber)), DwsFrontendPermissions.Read, token);

    [HttpGet("api/{id:guid}/validation")]
    public Task<IActionResult> Validate(Guid id, [FromQuery] int? revisionNumber, CancellationToken token) =>
        _proxy.GetAsync(HttpContext, Path(id, "validation", ("revisionNumber", revisionNumber)), DwsFrontendPermissions.Validate, token);

    [HttpGet("api/{id:guid}/revision-comparison")]
    public Task<IActionResult> CompareRevisions(Guid id, [FromQuery] int left, [FromQuery] int right, CancellationToken token) =>
        _proxy.GetAsync(HttpContext, Path(id, "revision-comparison", ("left", left), ("right", right)), DwsFrontendPermissions.Compare, token);

    [HttpGet("api/{id:guid}/baseline-comparison")]
    public Task<IActionResult> CompareBaselines(Guid id, [FromQuery] int left, [FromQuery] int right, CancellationToken token) =>
        _proxy.GetAsync(HttpContext, Path(id, "baseline-comparison", ("left", left), ("right", right)), DwsFrontendPermissions.Compare, token);

    private static string Path(Guid id, string operation, params (string Name, int? Value)[] values)
    {
        var query = string.Join('&', values.Where(value => value.Value.HasValue)
            .Select(value => $"{Uri.EscapeDataString(value.Name)}={value.Value!.Value}"));
        return $"{id:D}/{operation}{(query.Length == 0 ? string.Empty : $"?{query}")}";
    }
}
