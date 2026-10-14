using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0173-FU02 L-1 Location tree + detail (LOC2) — tenant shell MVC controller.
// Route is single-segment /LocationTree; views live under Views/Inventory/LocationTree/ (pack §9: L-1 is not a list,
// so it has its own folder). The tree is a custom component, not a DataTable: it never pages or filters, it expands
// ONE parent at a time (LOC-08) and hands over to the L-2 list above 500 children. Node details, history, the form,
// the status actions and bulk bins use the shared /Locations proxies (one implementation for both screens).
[Authorize]
[Route("LocationTree")]
public sealed class LocationTreeController : Controller
{
    private const string ViewRoot = "~/Views/Inventory/LocationTree";

    /// <summary>Above this many children the tree does not draw them; it links to the list filtered by the parent.</summary>
    public const int MaxChildrenInTree = 500;
    private const int PageSize = 200;

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<LocationsIndex> _localizer;
    private readonly ILogger<LocationTreeController> _logger;

    public LocationTreeController(
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<LocationsIndex> localizer,
        ILogger<LocationTreeController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without inventory.locations.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // The children of one node (or the company's Sites when parentId is empty), Archived only on request.
    // searchLocations pages are read until all children are in (≤ 500); above that the answer is "too many".
    [HttpGet("children")]
    public async Task<IActionResult> Children([FromQuery] string? parentId, [FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        Guid? parent = null;
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            if (!Guid.TryParse(parentId, out var parsed))
                return Refuse(StatusCodes.Status400BadRequest, LocationListAdapter.InvalidQuery, null);
            parent = parsed;
        }

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var nodes = new List<LocationTreeNodeViewModel>();
        var total = 0;
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync("/api/locations/search" + LocationListAdapter.ChildrenQuery(parent, includeArchived, page, PageSize), cancellationToken);
            if (!reply.IsSuccess)
            {
                _logger.LogWarning("Location tree children refused with {Status} {Code} (correlation {CorrelationId}).",
                    reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
                return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
            }

            var result = reply.Read<LocationSearchPageDto>() ?? new LocationSearchPageDto();
            var items = result.Items ?? [];
            total = result.Total ?? items.Count;
            if (total > MaxChildrenInTree)
                return Json(new { items = Array.Empty<LocationTreeNodeViewModel>(), total, tooMany = true });

            nodes.AddRange(items.Select(i => new LocationTreeNodeViewModel
            {
                Id = i.LocationId,
                Code = i.Code,
                Name = i.Name,
                Type = i.Type,
                ParentId = i.ParentId,
                Status = i.Status,
                IsStockable = i.IsStockable
            }));
            if (items.Count < PageSize || nodes.Count >= total) break;
        }

        return Json(new
        {
            items = nodes.Select(n => new { n.Id, n.Code, n.Name, n.Type, n.ParentId, n.Status, n.IsStockable, n.CanHaveChildren }),
            total,
            tooMany = false
        });
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        InventoryProxy.Error(status, code, _localizer[LocationErrorMessages.KeyFor(code, status)].Value, correlationId);
}
