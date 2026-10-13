using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Traceability.LotMigration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0174 P4 — Elle Girilmiş Lotları Aktarma (pack §11.5, TRACE §4.8 + 1.2.0). Tenant shell MVC; views under
// Views/Traceability/LotMigration/. The decisions live in the page until the ONE commit (ASSUMPTION-0174-11), so the page
// loads every group at once (client-mode list — "Tüm önerileri uygula" and the confirmation count need all of them).
// Same-origin proxy only: Gateway /api/trace through TraceGatewayClient. UAS-001 on trace.lots.migrate; the status read
// for the Stock Ledger (K4-1) needs only trace.lots.read.
[Authorize]
[Route("LotMigration")]
public sealed class LotMigrationController : Controller
{
    private const string ViewRoot = "~/Views/Traceability/LotMigration";

    /// <summary>listManualLotGroups pages fetched for the page (200 each): at most 10,000 groups on one screen.</summary>
    private const int MaxPages = 50;

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<LotMigrationIndex> _localizer;
    private readonly ILogger<LotMigrationController> _logger;

    public LotMigrationController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<LotMigrationIndex> localizer, ILogger<LotMigrationController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // Every typed group of the tenant with its product / SKU names and the suggested lot's number (one answer, the page
    // keeps it as its state). The service decides the groups, the counts and the suggestion (K-D); this only labels.
    [HttpGet("groups")]
    public async Task<IActionResult> Groups(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsMigrate)) return Refuse(403, null, null);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (trace is null || ledger is null) return Refuse(401, null, null);

        var groups = new List<ManualLotGroupDto>();
        ManualLotGroupPageDto? first = null;
        for (var page = 1; page <= MaxPages; page++)
        {
            var reply = await trace.GetAsync($"/api/trace/migration/manual-lots?view=all&page={page}&pageSize=200", cancellationToken);
            if (!reply.IsSuccess)
            {
                _logger.LogWarning("TRACE listManualLotGroups failed with {Status} (correlation {CorrelationId}).", reply.StatusCode, reply.CorrelationId);
                return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
            }

            var body = reply.Read<ManualLotGroupPageDto>() ?? new ManualLotGroupPageDto();
            first ??= body;
            groups.AddRange(body.Items ?? []);
            if ((body.Items?.Count ?? 0) < 200 || groups.Count >= (body.Total ?? 0)) break;
        }

        var labels = await TraceLabels.ResolveAsync(ledger, groups.Select(g => g.ItemId), groups.Select(g => (g.SkuId, g.SkuLevel)), [], cancellationToken);
        var lotNumbers = await LotNumbersAsync(trace, groups, cancellationToken);
        var text = groups.Where(g => g.GroupId is not null).ToDictionary(g => g.GroupId!, g => g.TypedText ?? string.Empty, StringComparer.Ordinal);
        return Json(new
        {
            data = new
            {
                items = groups.Select(g =>
                {
                    var normalized = LotFormRules.NormalizeLotNumber(g.TypedText ?? string.Empty);
                    return new
                    {
                        id = g.GroupId,
                        groupId = g.GroupId,
                        itemId = g.ItemId,
                        product = labels.Product(g.ItemId) ?? g.ItemId,
                        sku = $"{g.SkuLevel}:{g.SkuId}",
                        skuLabel = labels.Sku(g.SkuId) ?? g.SkuId,
                        typedText = g.TypedText,
                        normalized,
                        normalizedValid = LotFormRules.IsLotNumber(normalized),
                        transactionCount = g.TransactionCount ?? 0,
                        onHand = g.OnHand,
                        baseUomId = g.BaseUomId,
                        suggestion = new
                        {
                            kind = g.Suggestion?.Kind ?? "None",
                            note = g.Suggestion?.Note,
                            lotId = g.Suggestion?.LotId,
                            lotNumber = g.Suggestion?.LotId is { } id ? lotNumbers.GetValueOrDefault(id) : null,
                            groupId = g.Suggestion?.GroupId,
                            groupText = g.Suggestion?.GroupId is { } target ? text.GetValueOrDefault(target) : null
                        }
                    };
                }),
                total = groups.Count,
                filteredTotal = groups.Count
            },
            totals = new { groups = first?.Totals?.Groups ?? groups.Count, transactions = first?.Totals?.Transactions ?? groups.Sum(g => g.TransactionCount ?? 0) },
            completed = first?.Completed,
            labelsAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    // "Mevcut lota bağla" / "Yazım hatası": the registered lots of the group's SKU (lot no · SKT · status).
    [HttpGet("lots")]
    public async Task<IActionResult> Lots([FromQuery] string? sku, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsMigrate)) return Refuse(403, null, null);
        if (!LotFormRules.TryParseSku(sku, out var level, out var skuId)) return Json(new { items = Array.Empty<object>(), available = true });
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return Refuse(401, null, null);
        var reply = await trace.GetAsync($"/api/trace/lots?skuId={skuId}&skuLevel={level}&page=1&pageSize=200&sort=lotNumber", cancellationToken);
        var lots = reply.IsSuccess ? reply.Read<LotListDto>()?.Items ?? [] : [];
        return Json(new
        {
            items = lots.Select(l => new { value = l.LotId, lotNumber = l.LotNumber, expiryDate = l.ExpiryDate, status = l.Status }),
            available = reply.IsSuccess
        });
    }

    // The new-lot window's fields, checked with the Yeni Lot rules (the group fixes item + SKU).
    [HttpPost("validate-lot")]
    [ValidateAntiForgeryToken]
    public IActionResult ValidateLot([FromBody] LotFormInput? input)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsMigrate)) return Refuse(403, null, null);
        var lot = (input ?? new LotFormInput()) with { ItemId = Guid.Empty.ToString(), Sku = "Lsku:" + Guid.Empty };
        var errors = LotFormRules.Check(lot, create: true, currentStatus: null, DateOnly.FromDateTime(DateTime.UtcNow));
        return errors.Count == 0
            ? Json(new { ok = true, lotNumber = LotFormRules.NormalizeLotNumber(input!.LotNumber!) })
            : FieldErrors(errors);
    }

    // "Aktarımı Onayla": ONE commit with every decision; the key is the PAGE's (a double click replays, never commits twice).
    [HttpPost("commit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Commit([FromBody] MigrationCommitInput? input, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsMigrate)) return Refuse(403, null, null);
        if (input is null || !Guid.TryParse(input.IdempotencyKey, out var key)) return Refuse(400, "INVALID_REQUEST", null);
        var errors = LotMigrationRules.Check(input, DateOnly.FromDateTime(DateTime.UtcNow));
        if (errors.Count > 0) return FieldErrors(errors);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return Refuse(401, null, null);

        var serviceKey = "lot-migration-" + key.ToString("N");
        var reply = await trace.PostAsync("/api/trace/migration/manual-lots/commit", LotMigrationRules.Payload(input, serviceKey), serviceKey, null, cancellationToken);
        if (!reply.IsSuccess)
        {
            _logger.LogWarning("TRACE commitManualLotMigration refused with {Status} {Code} (correlation {CorrelationId}).",
                reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
            return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        }

        return Json(new { success = true, result = reply.Read<MigrationResultDto>() });
    }

    // CT K4-1: the Stock Ledger lists ask once whether typing is closed (their user's trace.lots.read; none → open).
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
        => Json(new { closed = await TraceMigrationStatus.IsClosedAsync(_httpClient, _gatewayUrl, Request, User, _permissions, cancellationToken) });

    private static async Task<Dictionary<string, string>> LotNumbersAsync(TraceGatewayClient trace, IReadOnlyList<ManualLotGroupDto> groups, CancellationToken ct)
    {
        var numbers = new Dictionary<string, string>(StringComparer.Ordinal);
        var skus = groups.Where(g => g.Suggestion?.LotId is not null).Select(g => (g.SkuId, g.SkuLevel)).Distinct();
        foreach (var (skuId, level) in skus)
        {
            var reply = await trace.GetAsync($"/api/trace/lots?skuId={skuId}&skuLevel={level}&page=1&pageSize=200", ct);
            foreach (var lot in reply.IsSuccess ? reply.Read<LotListDto>()?.Items ?? [] : [])
            {
                if (lot.LotId is not null && lot.LotNumber is not null) numbers[lot.LotId] = lot.LotNumber;
            }
        }

        return numbers;
    }

    private IActionResult FieldErrors(IReadOnlyDictionary<string, string> errors) =>
        new JsonResult(new
        {
            error = new { code = "INVALID_REQUEST", message = _localizer["Validation.FixFields"].Value, scope = "field" },
            fields = errors.ToDictionary(e => e.Key, e => _localizer[e.Value].Value),
            contractVersion = InventoryProxy.ContractVersion
        })
        { StatusCode = 400 };

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        TraceErrorMessages.Refuse(status, code, _localizer[TraceErrorMessages.KeyForMigration(code, status)].Value, correlationId);
}
