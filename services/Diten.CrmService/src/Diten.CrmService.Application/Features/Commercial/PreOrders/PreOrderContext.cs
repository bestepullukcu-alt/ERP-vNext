using System.Globalization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitEvidenceFlags;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>The caller's team (MOD-0168 §4.8; user decision 2026-10-21: the W5 / W3E territory-subtree team rule) — the rep
/// ids whose orders the caller sees beside their own. Null = every rep (read-all).</summary>
public interface IPreOrderTeamScope
{
    Task<IReadOnlySet<string>?> TeamRepsAsync(Guid tenantId, bool all, DateTimeOffset now, CancellationToken ct);
}

/// <summary>The existing visit-evidence team resolver behind <see cref="IPreOrderTeamScope"/> (consumed as is).</summary>
public sealed class TerritoryPreOrderTeamScope : IPreOrderTeamScope
{
    private readonly VisitEvidenceFlagScopeResolver _resolver;

    public TerritoryPreOrderTeamScope(VisitEvidenceFlagScopeResolver resolver) => _resolver = resolver;

    public async Task<IReadOnlySet<string>?> TeamRepsAsync(Guid tenantId, bool all, DateTimeOffset now, CancellationToken ct)
    {
        if (all)
        {
            return null;
        }

        var scope = await _resolver.ResolveAsync(tenantId, now, false, ct);
        return scope.Reps;
    }
}

/// <summary>Who sees an order (§4.8): read-all → every order; else the caller's own, their team's, and the orders they were
/// resolved as an approver of. Anything else answers 404 like a missing order.</summary>
public sealed record PreOrderVisibility(Guid? CallerId, bool All, IReadOnlySet<string>? TeamReps)
{
    public bool Sees(PreOrder order)
        => All || (CallerId is { } me && (order.Rep.UserId == me || order.ApprovalRequirement?.ApproverUserIds.Contains(me) == true))
           || (TeamReps?.Contains(order.Rep.UserId.ToString("D")) ?? false);

    public bool IsOwner(PreOrder order) => CallerId is { } me && order.Rep.UserId == me;

    /// <summary>The rep's manager: a resolved approver of the order or a team lead over the rep (never the rep).</summary>
    public bool IsManagerOf(PreOrder order)
        => CallerId is { } me && order.Rep.UserId != me
           && (order.ApprovalRequirement?.ApproverUserIds.Contains(me) == true || (TeamReps?.Contains(order.Rep.UserId.ToString("D")) ?? false));

    public bool IsApprover(PreOrder order) => CallerId is { } me && order.ApprovalRequirement?.ApproverUserIds.Contains(me) == true;
}

/// <summary>The selling side of a new order: the rep's primary seat → legal entity (W3L K-L3) → MDM, and the ORD prefix.</summary>
public sealed record PreOrderSellerContext(PreOrderLegalEntity LegalEntity, string? Prefix, Guid? PositionId, string? PositionCode);

/// <summary>
/// WP-VW-W6 W6-BE-a — the services every MOD-0168 handler shares: the caller, the clock, the tenant settings, the seller
/// context, visibility and the optimistic save. No state change happens here (that is <see cref="PreOrderStateMachine"/>).
/// </summary>
public sealed class PreOrderContext
{
    public PreOrderContext(ITenantContext tenant, ICallerScope caller, IActorContext actor, TimeProvider clock, IPreOrderRepository orders,
        IPreOrderSettingsRepository settings, IPreOrderTeamScope team, IOrganizationSeatReader seats, IPreOrderMdmDirectory mdm,
        IUserDisplayNameResolver names)
    {
        Tenant = tenant;
        Caller = caller;
        Actor = actor;
        Clock = clock;
        Orders = orders;
        Settings = settings;
        Team = team;
        Seats = seats;
        Mdm = mdm;
        Names = names;
    }

    public ITenantContext Tenant { get; }
    public ICallerScope Caller { get; }
    public IActorContext Actor { get; }
    public TimeProvider Clock { get; }
    public IPreOrderRepository Orders { get; }
    public IPreOrderSettingsRepository Settings { get; }
    public IPreOrderTeamScope Team { get; }
    public IOrganizationSeatReader Seats { get; }
    public IPreOrderMdmDirectory Mdm { get; }
    public IUserDisplayNameResolver Names { get; }

    public DateTimeOffset Now => Clock.GetUtcNow();

    /// <summary>"Today" in UTC (CRM has no tenant time zone yet — the MOD-0156 rule).</summary>
    public DateOnly Today => DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime);

    public Guid? CallerId => Guid.TryParse(Caller.CallerResourceId, out var id) && id != Guid.Empty ? id : null;

    public string? CallerText => CallerId?.ToString("D");

    public async Task<PreOrderSettings> SettingsAsync(Guid tenantId, CancellationToken ct)
        => await Settings.GetAsync(tenantId, ct) ?? new PreOrderSettings { TenantId = tenantId, Version = 0 };

    public static string TierFor(PreOrderSettings settings, string countryCode)
        => settings.PriceTierByCountry.FirstOrDefault(t => string.Equals(t.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))?.Tier
           ?? PreOrderSettingsDefaults.DefaultTier;

    public async Task<PreOrderVisibility> VisibilityAsync(Guid tenantId, CancellationToken ct)
    {
        var all = Caller.HasPermission(PreOrderPermissions.ReadAll);
        var team = await Team.TeamRepsAsync(tenantId, all, Now, ct);
        return new PreOrderVisibility(CallerId, all, team);
    }

    /// <summary>The order when it exists in the tenant AND the caller sees it; else null (404 — no leak).</summary>
    public async Task<(PreOrder? Order, PreOrderVisibility Visibility)> LoadVisibleAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var visibility = await VisibilityAsync(tenantId, ct);
        var order = await Orders.GetByIdAsync(tenantId, id, ct);
        return (order is not null && order.TenantId == tenantId && !order.IsDeleted && visibility.Sees(order) ? order : null, visibility);
    }

    public async Task<bool> SaveAsync(PreOrder order, int expectedVersion, CancellationToken ct)
    {
        order.UpdatedAt = Now;
        order.UpdatedBy = Actor.ActorName;
        return await Orders.ReplaceAsync(order, expectedVersion, ct);
    }

    public PreOrderAbilities Abilities(PreOrder o, PreOrderVisibility v)
        => new(
            Edit: v.IsOwner(o) && PreOrderStateMachine.IsEditable(o.Status) && Caller.HasPermission(PreOrderPermissions.Create),
            Submit: v.IsOwner(o) && PreOrderStateMachine.CanSubmit(o.Status) && Caller.HasPermission(PreOrderPermissions.Submit),
            Decide: o.Status == PreOrderStatuses.PendingApproval && v.IsApprover(o) && !v.IsOwner(o) && Caller.HasPermission(PreOrderPermissions.Approve),
            Cancel: PreOrderStateMachine.CanTransition(o.Status, PreOrderStateMachine.Actions.Cancel) && (v.IsOwner(o) || v.IsManagerOf(o))
                    && Caller.HasPermission(PreOrderPermissions.Cancel),
            Copy: PreOrderStateMachine.CanCopy(o.Status) && Caller.HasPermission(PreOrderPermissions.Create),
            RecordDepotResponse: PreOrderStateMachine.CanRecordDepotResponse(o.Status) && Caller.HasPermission(PreOrderPermissions.RecordDepotResponse),
            RetryTransmission: o.Status == PreOrderStatuses.TransmissionFailed && Caller.HasPermission(PreOrderPermissions.RetryTransmission));

    public PreOrderDto Dto(PreOrder o, PreOrderVisibility v) => PreOrderMapping.ToDto(o, Abilities(o, v));

    /// <summary>The caller's seat → legal entity → MDM (K-O4 / K-L3); a missing seat is 422, an unreachable one 503.</summary>
    public async Task<(PreOrderSellerContext? Seller, Response<T>? Error)> SellerAsync<T>(Guid tenantId, CancellationToken ct)
    {
        if (CallerId is not { } me)
        {
            return (null, PreOrderResults.Fail<T>(PreOrderErrors.NoSeat, "The caller's user id is required.", 422));
        }

        var seat = await Seats.GetAsync(me.ToString("D"), ct);
        if (!seat.Available)
        {
            return (null, PreOrderResults.ContextUnavailable<T>("The rep's seat (Platform)"));
        }

        if (seat.Primary?.LegalEntityId is not { } legalEntityId || legalEntityId == Guid.Empty)
        {
            return (null, PreOrderResults.Fail<T>(PreOrderErrors.NoSeat, "The rep has no primary seat in a legal entity today.", 422));
        }

        var (outcome, legalEntity) = await Mdm.GetLegalEntityAsync(legalEntityId, ct);
        if (outcome == PreOrderLookupOutcome.Unavailable)
        {
            return (null, PreOrderResults.ContextUnavailable<T>("The legal entity (MDM)"));
        }

        if (legalEntity is null || string.IsNullOrWhiteSpace(legalEntity.CountryCode) || string.IsNullOrWhiteSpace(legalEntity.BaseCurrencyCode))
        {
            return (null, PreOrderResults.Fail<T>(PreOrderErrors.NoSeat, "The seat's legal entity has no country / base currency in MDM.", 422));
        }

        var settings = await SettingsAsync(tenantId, ct);
        var prefix = settings.CompanyPrefixes.FirstOrDefault(p => p.LegalEntityId == legalEntityId)?.Prefix;
        return (new PreOrderSellerContext(legalEntity, prefix, seat.Primary.PositionId, seat.Primary.PositionCode), null);
    }

    public async Task<string?> DisplayNameAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var names = await Names.ResolveAsync([userId], ct);
            return names.TryGetValue(userId, out var name) ? name : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null; // fail-soft: the name is display data only
        }
    }
}

/// <summary>
/// The MOD-0156 bridge (§4.2): the SAME <see cref="IPricingEvaluator"/> call for the preview and the submission, its result
/// copied onto the lines. MOD-0168 does not compute a price, a VAT, a free box, a ratio or a discount (AC-PRICE-2).
/// </summary>
public sealed class PreOrderPricing
{
    private readonly IPricingEvaluator _evaluator;
    private readonly IPriceResolver _resolver;
    private readonly IFxRateReader _fx;

    public PreOrderPricing(IPricingEvaluator evaluator, IPriceResolver resolver, IFxRateReader fx)
    {
        _evaluator = evaluator;
        _resolver = resolver;
        _fx = fx;
    }

    /// <summary>The price mode of the (legal entity, country) today — a list in force ⇒ priced (K-O4).</summary>
    public Task<PriceResolution> ModeAsync(Guid tenantId, PreOrderLegalEntity seller, DateOnly today, string tier, CancellationToken ct)
        => _resolver.ResolveAsync(tenantId, seller.Id, seller.CountryCode, today, tier, Array.Empty<Guid>(), ct);

    public Task<EvaluationResult> EvaluateAsync(Guid tenantId, Guid legalEntityId, string countryCode, DateOnly today, string tier,
        IReadOnlyList<PreOrderLine> lines, CancellationToken ct)
        => _evaluator.EvaluateAsync(new EvaluationRequest(tenantId, legalEntityId, countryCode, today, tier,
            null, // §20.3 AÇIK-5: the first release has no pharmacy segment
            lines.Select(l => new EvaluationLine(l.LineNo, l.SkuId, l.Quantity, l.Mf is { } mf ? new MfInput(mf.Buy, mf.Free) : null, l.DiscountPct))
                .ToList()), ct);

    /// <summary>The evaluated numbers onto the order's lines + totals (copied, never recomputed).</summary>
    public static void Apply(PreOrder order, EvaluationResult result)
    {
        foreach (var line in order.Lines)
        {
            var evaluated = result.Lines.FirstOrDefault(l => l.LineNo == line.LineNo);
            if (evaluated is null)
            {
                continue;
            }

            line.UnitPrice = evaluated.UnitPrice;
            line.VatRate = evaluated.VatRate;
            line.NetAmount = evaluated.NetAmount;
            line.VatAmount = evaluated.VatAmount;
            line.FreeQuantity = evaluated.FreeQuantity;
            line.MfRatioPct = evaluated.MfRatioPct;
            line.CombinedDiscountPct = evaluated.CombinedDiscountPct;
            line.Outcome = evaluated.Outcome;
            line.Findings = evaluated.Findings.Select(Finding).ToList();
            line.RuleRef = evaluated.Rule is { } rule ? new PreOrderRuleRef { RuleKey = rule.RuleKey, RuleVersion = rule.RuleVersion } : null;
        }

        order.PriceMode = result.Mode == PricingModes.Priced ? PreOrderPriceModes.Priced : PreOrderPriceModes.Quantity;
        order.Currency = result.Mode == PricingModes.Priced ? result.Currency : null;
        order.Totals = result.Totals is { } t
            ? new PreOrderTotals { Net = t.Net, Vat = t.Vat, Gross = t.Gross, EffectiveDiscountPct = t.EffectiveDiscountPct }
            : null;
    }

    public static PreOrderFinding Finding(PricingFinding f)
        => new() { Code = f.Code, Severity = f.Severity, Params = f.Params.ToDictionary(p => p.Key, p => Text(p.Value)) };

    private static string? Text(object? value) => value switch
    {
        null => null,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    /// <summary>The local-currency equivalent (K-W10 g): only when the list currency differs from the local one and a rate
    /// exists on or before today; written at submission and never changed. No rate → null (the order is still taken).</summary>
    public async Task<PreOrderFxSnapshot?> FxAsync(Guid tenantId, Guid legalEntityId, string? currency, string localCurrency, decimal? gross,
        DateOnly today, CancellationToken ct)
    {
        if (currency is null || string.Equals(currency, localCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rate = await _fx.GetAsync(tenantId, legalEntityId, currency, localCurrency, today, ct);
        return rate is null
            ? null
            : new PreOrderFxSnapshot
            {
                LocalCurrency = localCurrency, Rate = rate.Rate, RateDate = rate.RateDate, Source = rate.Source,
                LocalGross = gross is { } g ? g * rate.Rate : null
            };
    }
}
