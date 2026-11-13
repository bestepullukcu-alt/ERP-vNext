using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

// WP-VW-W6 W6-BE-a — the doors MOD-0168 reads / acts through. MDM and Platform are read through the Gateway with the
// caller's token and FAIL CLOSED ("could not ask" is never "does not exist"). MOD-0156 is consumed in-process through
// its three §4.6 interfaces only (IPriceResolver, IPricingEvaluator, IFxRateReader).

public enum PreOrderLookupOutcome
{
    Found,
    NotFound,
    Unavailable
}

/// <summary><see cref="Address"/> / <see cref="TaxNumber"/> (W6-FIX-c, V-13): the registered address as one line and the tax
/// id, null when MDM carries none.</summary>
public sealed record PreOrderLegalEntity(Guid Id, string Code, string Name, string CountryCode, string BaseCurrencyCode, string? Address = null,
    string? TaxNumber = null);

/// <summary>One SKU as product-master v1.1 answers it (bulk-read + getSku): code, name, level, lifecycle, pack, GTIN.</summary>
public sealed record PreOrderSku(Guid SkuId, string SkuLevel, string Code, string Name, string LifecycleStatus, decimal? PackQuantity = null,
    string? PackUomCode = null, string? Gtin = null)
{
    public bool IsActive => string.Equals(LifecycleStatus, "Active", StringComparison.OrdinalIgnoreCase);
    public bool IsRetired => string.Equals(LifecycleStatus, "Retired", StringComparison.OrdinalIgnoreCase);
}

public interface IPreOrderMdmDirectory
{
    Task<(PreOrderLookupOutcome Outcome, PreOrderLegalEntity? LegalEntity)> GetLegalEntityAsync(Guid legalEntityId, CancellationToken ct);

    /// <summary>These SKUs (absent ids are not in the answer); null = MDM could not be asked.</summary>
    Task<IReadOnlyDictionary<Guid, PreOrderSku>?> ReadSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct);
}

public sealed record PreOrderLineManager(Guid UserId, string? DisplayName, Guid PositionId, string? PositionCode);

/// <summary>Platform PLT-W6 <c>seats/{userId}/line-managers</c>: <c>LineManager</c> | <c>ManagerChain</c> | <c>None</c>.</summary>
public sealed record PreOrderLineManagers(bool Available, string Resolution, Guid? PositionId, string? PositionCode, string? PositionName,
    IReadOnlyList<PreOrderLineManager> Managers)
{
    public static readonly PreOrderLineManagers Unavailable = new(false, "None", null, null, null, Array.Empty<PreOrderLineManager>());
}

public interface IPreOrderLineManagerReader
{
    /// <summary>3 s budget + one retry; never throws (Unavailable instead).</summary>
    Task<PreOrderLineManagers> GetAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// The e-mail events of §4.6 — W6-BE-c fills them (Platform Notifications + PDF). Never breaks the flow (K-N8): an
/// implementation swallows its own failures. The plain approval token is handed over here and nowhere else.
/// </summary>
public interface IPreOrderNotifier
{
    Task ApprovalRequestedAsync(PreOrder order, string approvalToken, DateTimeOffset expiresAt, CancellationToken ct);
    Task AutoApprovedAsync(PreOrder order, CancellationToken ct);
    Task ChangesRequestedAsync(PreOrder order, string reason, CancellationToken ct);
    Task RejectedAsync(PreOrder order, string reason, CancellationToken ct);
    Task CancelledAsync(PreOrder order, string reason, CancellationToken ct);
}

/// <summary><c>Sent</c> (with the dispatch ids and document refs) | <c>Failed</c> (with a reason code) | <c>Deferred</c> (no
/// transmitter is wired yet — the order stays <c>approved</c>, nothing is recorded).</summary>
public sealed record PreOrderTransmissionResult(string Outcome, IReadOnlyList<string> DispatchIds, Guid? DepotDocumentRef, Guid? PharmacyCopyRef,
    string? ReasonCode)
{
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Deferred = "deferred";

    public static readonly PreOrderTransmissionResult NotWired = new(Deferred, Array.Empty<string>(), null, null, "transmission_not_wired");
}

/// <summary>The depot document (PDF) + the depot / pharmacy e-mails of a transmission — W6-BE-c.</summary>
public interface IPreOrderTransmitter
{
    Task<PreOrderTransmissionResult> TransmitAsync(PreOrder order, int attemptNo, CancellationToken ct);
}

public interface IPreOrderApprovalSettings
{
    /// <summary><c>Commercial:PreOrders:Workflow:TemplateCode</c>, default <c>COM-PREORDER-APPROVAL</c>.</summary>
    string TemplateCode { get; }

    /// <summary>How old an open approval must be before a read asks MOD-0023 for its outcome (event bus fallback).</summary>
    int ReconcileAfterSeconds { get; }
}

public static class PreOrderApprovalDefaults
{
    public const string TemplateCode = "COM-PREORDER-APPROVAL";
    public const int ReconcileAfterSeconds = 120;
    public const string ObjectType = "commercial.pre-order";
    public const string ReasonPrefix = "COMMERCIAL_PRE_ORDER";

    public static string Link(Guid id) => $"/Commercial/PreOrders/Approve/{id:D}";
}

/// <summary>Account types (tenant BRD <c>account-type</c>): a pharmacy is <c>pharmacy</c>; a depot is a distributor or a
/// wholesaler (K-W13).</summary>
public static class PreOrderAccountTypes
{
    public const string Pharmacy = "pharmacy";
    public static readonly IReadOnlyList<string> Depot = new[] { "distributor", "wholesaler" };

    public static bool IsPharmacy(string? type) => string.Equals(type?.Trim(), Pharmacy, StringComparison.OrdinalIgnoreCase);

    public static bool IsDepot(string? type) => Depot.Contains(type?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
}
