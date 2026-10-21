using System.Globalization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Domain.Entities.Commercial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-c1 — the seven MOD-0168 §4.6 e-mail events. The package wrote them <c>commercial.pre-order.*</c>; the
/// Platform catalogue accepts no hyphen, so they are written without one (the W5 lesson). The Platform pins the SAME seven
/// strings (CrmManifestProvider.PreOrderEvents, CrmPreOrderTemplateTests) — either side changing one turns red.
/// </summary>
public static class PreOrderNotificationEvents
{
    public const string ApprovalRequested = "commercial.preorder.approvalrequested";
    public const string AutoApproved = "commercial.preorder.autoapproved";
    public const string ChangesRequested = "commercial.preorder.changesrequested";
    public const string Rejected = "commercial.preorder.rejected";
    public const string TransmittedDepot = "commercial.preorder.transmitteddepot";
    public const string PharmacyCopy = "commercial.preorder.pharmacycopy";
    public const string Cancelled = "commercial.preorder.cancelled";

    public static readonly IReadOnlyList<string> All =
        new[] { ApprovalRequested, AutoApproved, ChangesRequested, Rejected, TransmittedDepot, PharmacyCopy, Cancelled };

    /// <summary>The asking module on the internal dispatch: the manifest that declares these events (owner = CRM). The
    /// Platform's producer list speaks only for MOD-0155 today, so MOD-0168 names the owner itself.</summary>
    public const string SourceModule = "CRM";

    /// <summary>One send per (order, event, round[, part]) — the Platform opens no second dispatch for the same key
    /// (W5-FIX2). The round is the submission for a decision, the transmission attempt for a depot / pharmacy mail.</summary>
    public static string KeyOf(PreOrder order, string eventCode, int round, string? part = null)
        => $"preorder:{order.Id:N}:{eventCode[(eventCode.LastIndexOf('.') + 1)..]}:{round.ToString(CultureInfo.InvariantCulture)}"
           + (part is null ? string.Empty : ":" + part);
}

/// <summary>The Web links an e-mail carries (configured base URL + the page path; MOD-0168 §9).</summary>
public interface IPreOrderLinks
{
    /// <summary><c>/Commercial/PreOrders/Details/{id}</c>.</summary>
    string OrderUrl(Guid preOrderId);

    /// <summary><c>/Commercial/PreOrders/ApprovalLink?token=…</c> — signs in first (D4); the token is single-use.</summary>
    string ApprovalLinkUrl(string token);
}

/// <summary>The depot and pharmacy mails of a transmission (the transmitter decides the state from the depot one).</summary>
public interface IPreOrderTransmissionMailer
{
    Task<PlatformNotificationResult> SendDepotAsync(PreOrder order, int attemptNo, PreOrderDocuments documents, CancellationToken ct);

    /// <summary>Null when the pharmacy has no e-mail (nothing to send).</summary>
    Task<PlatformNotificationResult?> SendPharmacyCopyAsync(PreOrder order, int attemptNo, PreOrderDocuments documents, CancellationToken ct);
}

/// <summary>
/// WP-VW-W6 W6-BE-c1 — MOD-0168 §4.6 through the Platform's internal event dispatch (the W5 client: 3 s + one retry, never
/// throws, the row's own idempotency key). An e-mail failure NEVER breaks the flow (K-N8): the decision / submission /
/// cancellation stands whatever the mail did; only the depot mail decides a transmission (the transmitter reads it).
/// Recipients: internal users by user id (their own language); the approvers as resolved at submission; the line manager
/// by <c>lineManagerOf</c> the rep; the depot and the pharmacy (external, no language of their own) by their business
/// e-mail in the selling company's country language (decision B). Variables are display text; no address, no token
/// and no discount percentage ever goes into the log.
/// </summary>
public sealed class PlatformPreOrderNotifier : IPreOrderNotifier, IPreOrderTransmissionMailer
{
    private readonly IPlatformNotificationClient _platform;
    private readonly IPreOrderLinks _links;
    private readonly ILogger<PlatformPreOrderNotifier> _logger;

    public PlatformPreOrderNotifier(IPlatformNotificationClient platform, IPreOrderLinks links, ILogger<PlatformPreOrderNotifier>? logger = null)
    {
        _platform = platform;
        _links = links;
        _logger = logger ?? NullLogger<PlatformPreOrderNotifier>.Instance;
    }

    public Task ApprovalRequestedAsync(PreOrder order, string approvalToken, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var to = order.ApprovalRequirement?.ApproverUserIds.Select(id => new PlatformNotificationRecipient(UserId: id.ToString("D"))).ToList() ?? [];
        var vars = Internal(order);
        vars["approvalUrl"] = _links.ApprovalLinkUrl(approvalToken);
        vars["expiresAt"] = expiresAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        return SendQuietlyAsync(order, PreOrderNotificationEvents.ApprovalRequested, order.SubmissionNo, null, to, [], vars, ct);
    }

    public Task AutoApprovedAsync(PreOrder order, CancellationToken ct)
        => SendQuietlyAsync(order, PreOrderNotificationEvents.AutoApproved, order.SubmissionNo, null, [Rep(order)], [ManagerOf(order)], Internal(order), ct);

    public Task ChangesRequestedAsync(PreOrder order, string reason, CancellationToken ct)
        => SendQuietlyAsync(order, PreOrderNotificationEvents.ChangesRequested, order.SubmissionNo, null, [Rep(order)], [], WithReason(Internal(order), reason), ct);

    public Task RejectedAsync(PreOrder order, string reason, CancellationToken ct)
        => SendQuietlyAsync(order, PreOrderNotificationEvents.Rejected, order.SubmissionNo, null, [Rep(order)], [], WithReason(Internal(order), reason), ct);

    /// <summary>The rep and the manager always; the depot and the pharmacy ONLY when the order had been transmitted (D5,
    /// AC-FLOW-8 — the cancellation records it). Each external party gets its own mail: nobody sees another's address.</summary>
    public async Task CancelledAsync(PreOrder order, string reason, CancellationToken ct)
    {
        await SendQuietlyAsync(order, PreOrderNotificationEvents.Cancelled, 1, "internal", [Rep(order)], [ManagerOf(order)],
            WithReason(Internal(order), reason), ct);
        if (order.Cancellation?.NotifiedDepot != true)
        {
            return;
        }

        var external = WithReason(External(order), reason);
        if (Email(order.Depot?.Email) is { } depot)
        {
            await SendQuietlyAsync(order, PreOrderNotificationEvents.Cancelled, 1, "depot", [ExternalRecipient(order, depot)], [], external, ct);
        }

        if (order.Cancellation.NotifiedPharmacy && Email(order.Pharmacy.Email) is { } pharmacy)
        {
            await SendQuietlyAsync(order, PreOrderNotificationEvents.Cancelled, 1, "pharmacy", [ExternalRecipient(order, pharmacy)], [], external, ct);
        }
    }

    public async Task<PlatformNotificationResult> SendDepotAsync(PreOrder order, int attemptNo, PreOrderDocuments documents, CancellationToken ct)
    {
        if (Email(order.Depot?.Email) is not { } depot)
        {
            return PlatformNotificationResult.Unavailable(PreOrderTransmissionReasons.DepotNoEmail);
        }

        var vars = External(order);
        vars["note"] = order.Note?.Trim() ?? string.Empty;
        return await SendAsync(order, PreOrderNotificationEvents.TransmittedDepot, attemptNo, null, [ExternalRecipient(order, depot)], [], vars, ct);
    }

    public async Task<PlatformNotificationResult?> SendPharmacyCopyAsync(PreOrder order, int attemptNo, PreOrderDocuments documents, CancellationToken ct)
        => Email(order.Pharmacy.Email) is { } pharmacy
            ? await SendAsync(order, PreOrderNotificationEvents.PharmacyCopy, attemptNo, null, [ExternalRecipient(order, pharmacy)], [], External(order), ct)
            : null;

    // ── the wire ─────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task SendQuietlyAsync(PreOrder order, string eventCode, int round, string? part,
        IReadOnlyList<PlatformNotificationRecipient> to, IReadOnlyList<PlatformNotificationRecipient> cc, Dictionary<string, string> vars,
        CancellationToken ct)
    {
        var result = await SendAsync(order, eventCode, round, part, to, cc, vars, ct);
        if (!result.Available)
        {
            // K-N8 — logged by name, never thrown: the order's flow already stands
            _logger.LogWarning("pre_order.notify.failed PreOrderId={Id} Event={Event} Reason={Reason}", order.Id, eventCode, result.Error);
        }
    }

    private async Task<PlatformNotificationResult> SendAsync(PreOrder order, string eventCode, int round, string? part,
        IReadOnlyList<PlatformNotificationRecipient> to, IReadOnlyList<PlatformNotificationRecipient> cc, Dictionary<string, string> vars,
        CancellationToken ct)
    {
        if (to.Count == 0 && cc.Count == 0)
        {
            return new PlatformNotificationResult(true, Domain.Entities.VisitNotificationStatuses.NoRecipient, [], [], []);
        }

        try
        {
            return await _platform.DispatchAsync(new PlatformNotificationRequest(order.TenantId, eventCode, PreOrderNotificationEvents.SourceModule,
                to, cc, vars, $"crm:preorder:{order.Id:D}", order.Id.ToString("D"),
                ReplyToUserId: order.Rep.UserId == Guid.Empty ? null : order.Rep.UserId.ToString("D"),
                IdempotencyKey: PreOrderNotificationEvents.KeyOf(order, eventCode, round, part)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "pre_order.notify.threw PreOrderId={Id} Event={Event}", order.Id, eventCode);
            return PlatformNotificationResult.Unavailable("notifier_error");
        }
    }

    // ── recipients and variables ─────────────────────────────────────────────────────────────────────────────────

    private static PlatformNotificationRecipient Rep(PreOrder order) => new(UserId: order.Rep.UserId.ToString("D"));

    private static PlatformNotificationRecipient ManagerOf(PreOrder order) => new(LineManagerOf: order.Rep.UserId.ToString("D"));

    /// <summary>An external party: its business e-mail, in the selling company's country language (null = the tenant's).</summary>
    private static PlatformNotificationRecipient ExternalRecipient(PreOrder order, string email)
        => new(Email: email, Locale: ExternalRecipientLanguages.ForCountry(order.CountryCode));

    private static string? Email(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>What every pre-order mail shows (display text; the Platform template words it).</summary>
    private static Dictionary<string, string> Common(PreOrder order) => new(StringComparer.Ordinal)
    {
        ["orderCode"] = order.Code,
        ["pharmacyName"] = order.Pharmacy.Name?.Trim() ?? string.Empty,
        ["depotName"] = order.Depot?.Name?.Trim() ?? string.Empty,
        ["deliveryDate"] = order.RequestedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        ["lineCount"] = order.Lines.Count.ToString(CultureInfo.InvariantCulture),
        // the gross in the list currency (priced mode); a quantity order shows no amount — the depot prices it
        ["totalText"] = order.Totals is { } totals && !string.IsNullOrWhiteSpace(order.Currency)
            ? totals.Gross.ToString("0.00", CultureInfo.InvariantCulture) + " " + order.Currency
            : string.Empty,
        ["sellerName"] = order.Seller.Name?.Trim() ?? string.Empty
    };

    /// <summary>An internal mail: the rep's name and the link to the order in CRM.</summary>
    private Dictionary<string, string> Internal(PreOrder order)
    {
        var vars = Common(order);
        vars["repName"] = order.Rep.DisplayName?.Trim() ?? string.Empty;
        vars["orderUrl"] = _links.OrderUrl(order.Id);
        return vars;
    }

    /// <summary>An external mail: no CRM link (an empty orderUrl hides the button) and no person's name.</summary>
    private static Dictionary<string, string> External(PreOrder order)
    {
        var vars = Common(order);
        vars["orderUrl"] = string.Empty;
        return vars;
    }

    private static Dictionary<string, string> WithReason(Dictionary<string, string> vars, string reason)
    {
        vars["reason"] = reason?.Trim() ?? string.Empty;
        return vars;
    }
}

/// <summary>The reason codes a transmission attempt records (never a sentence).</summary>
public static class PreOrderTransmissionReasons
{
    public const string DepotNoEmail = "depot_no_email";
    public const string DocumentFailed = "document_failed";
}

/// <summary>The two documents of a transmission (MOD-0262 refs) — or why there are none.</summary>
public sealed record PreOrderDocuments(bool Ok, Guid? DepotDocumentRef, Guid? PharmacyCopyRef, string? ReasonCode = null)
{
    public static PreOrderDocuments Failed(string reasonCode) => new(false, null, null, reasonCode);
}

/// <summary>
/// The depot document (depot copy + pharmacy copy PDF, MOD-0262) of one transmission attempt — W6-BE-c2. Until it is
/// registered there is NO producer and the transmitter answers "not wired": the approved order waits, nothing is faked.
/// </summary>
public interface IPreOrderDocumentProducer
{
    Task<PreOrderDocuments> ProduceAsync(PreOrder order, int attemptNo, CancellationToken ct);
}

/// <summary>
/// WP-VW-W6 W6-BE-c1 — the system transmission (MOD-0168 §1.1 step 5): the documents, then the depot mail — its answer
/// decides the attempt (handed over / queued → sent; unavailable / uncertain → failed, retried) — then the pharmacy copy
/// (best effort: it never fails the attempt). Without a document producer the transmitter is OFF.
/// </summary>
public sealed class PreOrderTransmitter : IPreOrderTransmitter
{
    private readonly IPreOrderTransmissionMailer _mailer;
    private readonly IPreOrderDocumentProducer? _documents;
    private readonly ILogger<PreOrderTransmitter> _logger;

    public PreOrderTransmitter(IPreOrderTransmissionMailer mailer, IPreOrderDocumentProducer? documents = null, ILogger<PreOrderTransmitter>? logger = null)
    {
        _mailer = mailer;
        _documents = documents;
        _logger = logger ?? NullLogger<PreOrderTransmitter>.Instance;
    }

    /// <summary>False until W6-BE-c2 registers the document producer.</summary>
    public bool IsWired => _documents is not null;

    public async Task<PreOrderTransmissionResult> TransmitAsync(PreOrder order, int attemptNo, CancellationToken ct)
    {
        if (_documents is null)
        {
            return PreOrderTransmissionResult.NotWired;
        }

        var documents = await _documents.ProduceAsync(order, attemptNo, ct);
        if (!documents.Ok)
        {
            return new PreOrderTransmissionResult(PreOrderTransmissionResult.Failed, Array.Empty<string>(), null, null,
                documents.ReasonCode ?? PreOrderTransmissionReasons.DocumentFailed);
        }

        var depot = await _mailer.SendDepotAsync(order, attemptNo, documents, ct);
        if (!depot.Available)
        {
            return new PreOrderTransmissionResult(PreOrderTransmissionResult.Failed, Array.Empty<string>(), documents.DepotDocumentRef,
                documents.PharmacyCopyRef, depot.Error ?? "platform-unavailable");
        }

        var ids = depot.DispatchIds.ToList();
        var pharmacy = await _mailer.SendPharmacyCopyAsync(order, attemptNo, documents, ct);
        if (pharmacy is { Available: true })
        {
            ids.AddRange(pharmacy.DispatchIds);
        }
        else if (pharmacy is not null)
        {
            _logger.LogWarning("pre_order.transmission.pharmacy_copy_failed PreOrderId={Id} Reason={Reason}", order.Id, pharmacy.Error);
        }

        return new PreOrderTransmissionResult(PreOrderTransmissionResult.Sent, ids, documents.DepotDocumentRef, documents.PharmacyCopyRef, null);
    }
}
