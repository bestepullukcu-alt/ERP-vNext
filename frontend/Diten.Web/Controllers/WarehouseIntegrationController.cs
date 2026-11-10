using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.WarehouseIntegration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-7 — Entegrasyon İzleme (WAREHOUSEINTEGRATION, pack §8.23 / §11.1 / §11.5): ONLY the four summary cards, READ-ONLY — not a DataTable (r23,
// son bakış F-15). The PAGE is /Warehouse/Integration (the manifest route). The cards are drawn by the SERVER: the page action reads Gateway 5000
// /api/warehouse/integration/status (the DL-8 {everything} route) AFTER the key check and hands the values to the view, so the page never shows a
// "loading" title and a failed read shows only its reason. Without warehouse.integration-monitor.read nothing is read and the view draws only the
// access explanation (UAS-001, no redirect). The JSON action is the same read for a refresh. No answer carries a ship-to (Q-10a).
[Authorize]
[Route("Warehouse/WarehouseIntegration")]
public sealed class WarehouseIntegrationController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/WarehouseIntegration";
    private const string Base = "/api/warehouse/integration/status";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<WarehouseIntegrationIndex> _localizer;
    private readonly ILogger<WarehouseIntegrationController> _logger;

    public WarehouseIntegrationController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<WarehouseIntegrationIndex> localizer, ILogger<WarehouseIntegrationController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("~/Warehouse/Integration")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // UAS-001: no key → no read, the view draws _AccessDenied only
        if (!_permissions.Has(WarehousePermissionKeys.IntegrationMonitorRead)) return View($"{ViewRoot}/Index.cshtml");
        return View($"{ViewRoot}/Index.cshtml", await ReadAsync(ct));
    }

    [HttpGet("api")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.IntegrationMonitorRead)) return Refuse(403, null, null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync(Base, ct);
        if (!reply.IsSuccess) return Failed(reply);
        return Json(new { data = reply.Read<WmsIntegrationStatusDto>() ?? new WmsIntegrationStatusDto(), contractVersion = InventoryProxy.ContractVersion });
    }

    private async Task<WarehouseIntegrationViewModel> ReadAsync(CancellationToken ct)
    {
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return new WarehouseIntegrationViewModel { Error = _localizer[WarehouseErrorMessages.SessionExpired].Value };
        var reply = await gateway.GetAsync(Base, ct);
        if (reply.IsSuccess && reply.Read<WmsIntegrationStatusDto>() is { } status) return new WarehouseIntegrationViewModel { Status = status };
        if (reply.Error?.Code == InventoryGatewayClient.CompanyNotSelected) return new WarehouseIntegrationViewModel { CompanyNotSelected = true };
        _logger.LogWarning("Warehouse integration status refused with {Status} {Code} (correlation {CorrelationId}).",
            reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        var key = reply.IsSuccess ? WarehouseErrorMessages.Generic : WarehouseErrorMessages.KeyFor(reply.Error?.Code, reply.StatusCode);
        return new WarehouseIntegrationViewModel { Error = _localizer[key].Value };
    }

    private IActionResult Failed(GatewayReply reply)
    {
        _logger.LogWarning("Warehouse integration status refused with {Status} {Code} (correlation {CorrelationId}).",
            reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyFor(code, status)].Value, correlationId);
}
