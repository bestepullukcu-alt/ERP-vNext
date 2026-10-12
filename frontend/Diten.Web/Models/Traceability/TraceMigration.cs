using System.Security.Claims;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;

namespace Diten.Web.Models.Traceability;

// ── TRACE §4.8 + 1.2.0 wire (read side of the MVC proxy) ─────────────────────────────────────────────────────

public sealed class ManualLotGroupPageDto
{
    public List<ManualLotGroupDto>? Items { get; set; }
    public long? Total { get; set; }
    public ManualLotTotalsDto? Totals { get; set; }
    public MigrationResultDto? Completed { get; set; }
}

public sealed class ManualLotGroupDto
{
    public string? GroupId { get; set; }
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? TypedText { get; set; }
    public int? TransactionCount { get; set; }
    public string? OnHand { get; set; }
    public string? BaseUomId { get; set; }
    public ManualLotSuggestionDto? Suggestion { get; set; }
}

public sealed class ManualLotSuggestionDto
{
    public string? Kind { get; set; }
    public string? LotId { get; set; }
    public string? GroupId { get; set; }
    public string? Note { get; set; }
}

public sealed class ManualLotTotalsDto
{
    public int? Groups { get; set; }
    public int? Transactions { get; set; }
}

public sealed class MigrationResultDto
{
    public string? MigrationId { get; set; }
    public string? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public int? TransactionsLinked { get; set; }
    public int? LotsLinked { get; set; }
    public int? LotsCreated { get; set; }
}

public sealed class ManualLotMigrationStatusDto
{
    public bool ManualLotEntryClosed { get; set; }
    public string? CompletedAt { get; set; }
}

// ── the page's commit (as lotmigration/index.js posts it) ───────────────────────────────────────────────────

public sealed record MigrationCommitInput
{
    public string? IdempotencyKey { get; init; }
    public string? Reason { get; init; }
    public int? ExpectedGroupCount { get; init; }
    public IReadOnlyList<MigrationDecisionInput>? Decisions { get; init; }
}

/// <summary>One page decision: <c>LinkExisting</c> (lotId) · <c>Typo</c> (targetGroupId) · <c>NewLot</c> (the 9-field lot form).</summary>
public sealed record MigrationDecisionInput
{
    public string? GroupId { get; init; }
    public string? Kind { get; init; }
    public string? LotId { get; init; }
    public string? TargetGroupId { get; init; }
    public LotFormInput? NewLot { get; init; }
}

/// <summary>
/// WP-0174-P4 — the MVC checks of the migration commit (pack §11.5, §12 R), before anything reaches the Gateway; the
/// service repeats them and stays the authority (stale, incomplete, typo chains, clashes are its decisions). Field / row
/// → resx key. A new lot uses the SAME 9-field rules as the Yeni Lot form (<see cref="LotFormRules.Check"/>).
/// </summary>
public static class LotMigrationRules
{
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal) { "LinkExisting", "Typo", "NewLot" };

    public static IReadOnlyDictionary<string, string> Check(MigrationCommitInput input, DateOnly today)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if ((input.Reason?.Trim().Length ?? 0) < LotFormRules.MinReasonLength) errors["reason"] = "Validation.ReasonMin";
        if (input.ExpectedGroupCount is not >= 0) errors["expectedGroupCount"] = "Validation.Required";
        foreach (var decision in input.Decisions ?? [])
        {
            var row = "decision:" + decision.GroupId;
            if (string.IsNullOrWhiteSpace(decision.GroupId) || decision.Kind is null || !Kinds.Contains(decision.Kind))
            {
                errors[row] = "Validation.Required";
                continue;
            }

            if (decision.Kind == "LinkExisting" && !Guid.TryParse(decision.LotId, out _)) errors[row] = "Validation.Required";
            if (decision.Kind == "Typo" && string.IsNullOrWhiteSpace(decision.TargetGroupId)) errors[row] = "Validation.Required";
            if (decision.Kind == "NewLot")
            {
                // The group fixes item + SKU; only the lot's own fields are the page's.
                var lot = (decision.NewLot ?? new LotFormInput()) with { ItemId = Guid.Empty.ToString(), Sku = "Lsku:" + Guid.Empty };
                var fieldErrors = LotFormRules.Check(lot, create: true, currentStatus: null, today);
                if (fieldErrors.Count > 0) errors[row] = fieldErrors.Values.First();
            }
        }

        return errors;
    }

    /// <summary>The <c>CommitManualLotMigrationRequest</c> body (lot number normalised, dates as the contract's midnight UTC).</summary>
    public static object Payload(MigrationCommitInput input, string key) => new
    {
        idempotencyKey = key,
        reason = input.Reason!.Trim(),
        expectedGroupCount = input.ExpectedGroupCount!.Value,
        decisions = (input.Decisions ?? []).Select(d => d.Kind switch
        {
            "LinkExisting" => (object)new { groupId = d.GroupId, kind = d.Kind, lotId = Guid.Parse(d.LotId!).ToString("D") },
            "Typo" => new { groupId = d.GroupId, kind = d.Kind, targetGroupId = d.TargetGroupId },
            _ => new
            {
                groupId = d.GroupId,
                kind = d.Kind,
                newLot = new
                {
                    lotNumber = LotFormRules.NormalizeLotNumber(d.NewLot!.LotNumber!),
                    manufacturerLot = Blank(d.NewLot.ManufacturerLot),
                    supplierLot = Blank(d.NewLot.SupplierLot),
                    manufacturingDate = LotFormRules.Wire(d.NewLot.ManufacturingDate),
                    expiryDate = LotFormRules.Wire(d.NewLot.ExpiryDate),
                    retestDate = LotFormRules.Wire(d.NewLot.RetestDate),
                    countryOfOrigin = Blank(d.NewLot.CountryOfOrigin)?.ToUpperInvariant(),
                    coaReference = Blank(d.NewLot.CoaReference)
                }
            }
        }).ToList()
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// CT K4-1 — is the tenant's typed-lot entry closed? Read with the user's OWN <c>trace.lots.read</c> through
/// <c>getManualLotMigrationStatus</c>. Without that key, without a company, or when the answer cannot be read, the
/// Stock Ledger keeps today's behaviour (false) — a closed tenant's service refuses a typed inbound lot anyway (UNKNOWN_LOT).
/// </summary>
public static class TraceMigrationStatus
{
    public static async Task<bool> IsClosedAsync(HttpClient http, string gatewayUrl, HttpRequest request, ClaimsPrincipal user,
        IPermissionSnapshot permissions, CancellationToken cancellationToken)
    {
        if (!permissions.Has(TracePermissionKeys.LotsRead)) return false;
        var trace = TraceGatewayClient.For(http, gatewayUrl, request, user);
        if (trace is null) return false;
        try
        {
            var reply = await trace.GetAsync("/api/trace/migration/status", cancellationToken);
            return reply.IsSuccess && reply.Read<ManualLotMigrationStatusDto>()?.ManualLotEntryClosed == true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}

/// <summary>
/// FU-0174-P3-07 (K-F, ASSUMPTION-0174-06): the retest confirmation of a registered lot is enforced by the MVC form post
/// too, not only by the script. The script confirms with the header <see cref="Header"/> = <c>true</c> (the FU01 submit
/// model is not a P4 path); a registered lot whose retest date passed without it → refused on the lot field.
/// </summary>
public static class LotRetestGate
{
    public const string Header = "X-Lot-Retest-Confirmed";

    public const string ErrorKey = "Lot.RetestRequired";

    /// <summary>
    /// True = the post may go on. A typed lot / no lot passes; a registered lot is read with the user's
    /// <c>trace.lots.read</c> (a user without it never saw the picker's warning: passes, the service decides).
    /// </summary>
    public static async Task<bool> AllowsAsync(string? lotId, bool confirmed, HttpClient http, string gatewayUrl, HttpRequest request,
        ClaimsPrincipal user, IPermissionSnapshot permissions, CancellationToken cancellationToken)
    {
        if (confirmed || !MovementRules.IsRegisteredLotId(lotId) || !permissions.Has(TracePermissionKeys.LotsRead)) return true;
        var trace = TraceGatewayClient.For(http, gatewayUrl, request, user);
        if (trace is null) return true;
        var reply = await trace.GetAsync($"/api/trace/lots/{lotId}", cancellationToken);
        return !(reply.IsSuccess && reply.Read<LotRefDto>()?.RetestDue == true);
    }

    public static bool Confirmed(HttpRequest request)
        => string.Equals(request.Headers[Header].ToString(), "true", StringComparison.OrdinalIgnoreCase);
}
