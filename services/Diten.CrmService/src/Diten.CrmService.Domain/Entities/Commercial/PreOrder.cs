namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §4.1: the pharmacy pre-order. A TRANSFER order (K-O3): it is passed to the chosen depot,
/// the depot invoices; no stock moves. The seller, the country and the currencies come from the rep's primary seat and
/// MDM (K-O4) — never from the payload. Prices, VAT, free boxes and the guardrail outcome come from MOD-0156 and are
/// FIXED at submission (D12). The event log is append-only (DESIGN-GAP §3); the state changes live in
/// <c>PreOrderStateMachine</c> only.
/// </summary>
public sealed class PreOrder : EntityBase
{
    /// <summary><c>{PREFIX}-ORD-000123</c> — given when the draft is first saved (D15); never reused (RCS-001 §5.8).</summary>
    public string Code { get; set; } = string.Empty;

    public string Status { get; set; } = PreOrderStatuses.Draft;
    public string Fulfilment { get; set; } = PreOrderFulfilments.Depot;

    /// <summary><c>priced</c> | <c>quantity</c> — live on the draft, fixed at submission (MOD-0156 decides).</summary>
    public string PriceMode { get; set; } = PreOrderPriceModes.Quantity;

    public PreOrderSeller Seller { get; set; } = new();
    public string CountryCode { get; set; } = string.Empty;
    public string LocalCurrencyCode { get; set; } = string.Empty;

    /// <summary>The price list's currency (priced mode only).</summary>
    public string? Currency { get; set; }

    public PreOrderParty Pharmacy { get; set; } = new();
    public PreOrderParty? Depot { get; set; }
    public PreOrderRep Rep { get; set; } = new();
    public Guid? VisitId { get; set; }
    public DateOnly? RequestedDeliveryDate { get; set; }
    public string? Note { get; set; }
    public List<PreOrderLine> Lines { get; set; } = new();
    public PreOrderTotals? Totals { get; set; }
    public PreOrderFxSnapshot? Fx { get; set; }
    public PreOrderPriceSnapshot? Price { get; set; }
    public PreOrderEvaluationSnapshot? Evaluation { get; set; }
    public PreOrderApprovalRequirement? ApprovalRequirement { get; set; }
    public PreOrderSignature? Signature { get; set; }
    public PreOrderApproval? Approval { get; set; }
    public PreOrderTransmission? Transmission { get; set; }
    public PreOrderCancellation? Cancellation { get; set; }
    public PreOrderDepotResponse? DepotResponse { get; set; }
    public Guid? CopiedFromPreOrderId { get; set; }

    /// <summary>How many times the order was submitted (an approval link binds to one submission, §4.4).</summary>
    public int SubmissionNo { get; set; }

    /// <summary>Append-only (DESIGN-GAP §3) — a change request does not remove a row.</summary>
    public List<PreOrderEvent> Events { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>The selling legal entity (MDM) as it was when the draft was opened.</summary>
public sealed class PreOrderSeller
{
    public Guid LegalEntityId { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>A pharmacy / depot (MOD-0149 Account) snapshot; the fields the account does not carry stay null.</summary>
public sealed class PreOrderParty
{
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public string? AccountType { get; set; }
    public string? CityRef { get; set; }
    public string? Address { get; set; }
    public string? Gln { get; set; }
    public string? PharmacyCode { get; set; }
    public string? TaxNumber { get; set; }

    /// <summary>The account's business e-mail (the depot needs one to be sent the order) — business data, not a user's.</summary>
    public string? Email { get; set; }

    public string? SegmentCode { get; set; }
}

public sealed class PreOrderRep
{
    public Guid UserId { get; set; }
    public string? DisplayName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionCode { get; set; }
}

public sealed class PreOrderLine
{
    public int LineNo { get; set; }
    public Guid SkuId { get; set; }
    public string SkuLevel { get; set; } = string.Empty;
    public PreOrderSkuSnapshot Sku { get; set; } = new();
    public int Quantity { get; set; }
    public PreOrderMf? Mf { get; set; }
    public int FreeQuantity { get; set; }
    public decimal? DiscountPct { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? MfRatioPct { get; set; }
    public decimal? CombinedDiscountPct { get; set; }
    public string Outcome { get; set; } = "ok";
    public List<PreOrderFinding> Findings { get; set; } = new();
    public PreOrderRuleRef? RuleRef { get; set; }
}

public sealed class PreOrderSkuSnapshot
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>product-master v1.1 has no dosage form (null until MDM carries it).</summary>
    public string? Form { get; set; }

    public decimal? PackQuantity { get; set; }
    public string? PackUomCode { get; set; }
    public string? Gtin { get; set; }
}

public sealed class PreOrderMf
{
    public int Buy { get; set; }
    public int Free { get; set; }
}

/// <summary>A MOD-0156 reason code with its parameters (invariant text) — never a sentence (AC-PRICE-4).</summary>
public sealed class PreOrderFinding
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Dictionary<string, string?> Params { get; set; } = new();
}

public sealed class PreOrderRuleRef
{
    public Guid RuleKey { get; set; }
    public int RuleVersion { get; set; }
}

public sealed class PreOrderTotals
{
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Gross { get; set; }
    public decimal EffectiveDiscountPct { get; set; }
}

public sealed class PreOrderFxSnapshot
{
    public string LocalCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateOnly RateDate { get; set; }
    public string Source { get; set; } = string.Empty;

    /// <summary>Gross × rate, NOT rounded (MOD-0168 never rounds; the display formats).</summary>
    public decimal? LocalGross { get; set; }
}

public sealed class PreOrderPriceSnapshot
{
    public Guid PriceListId { get; set; }
    public int PriceListVersion { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public DateOnly ValidFrom { get; set; }
    public string Tier { get; set; } = string.Empty;
}

public sealed class PreOrderEvaluationSnapshot
{
    public string Outcome { get; set; } = "ok";
    public DateTimeOffset EvaluatedAt { get; set; }
    public string RuleSetFingerprint { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
}

public sealed class PreOrderApprovalRequirement
{
    public bool Required { get; set; }

    /// <summary><c>tenant-setting</c> | <c>soft-exceeded</c>.</summary>
    public List<string> Reasons { get; set; } = new();

    public Guid? ApproverPositionId { get; set; }
    public string? ApproverPositionCode { get; set; }
    public List<Guid> ApproverUserIds { get; set; } = new();
    public Dictionary<string, string> ApproverNames { get; set; } = new();

    /// <summary>Platform's resolution: <c>LineManager</c> | <c>ManagerChain</c> (a vacant seat moved up) | <c>unresolved</c>
    /// (Platform could not be asked — the order waits and the approvers are resolved again on read).</summary>
    public string FallbackLevel { get; set; } = string.Empty;
}

public sealed class PreOrderSignature
{
    /// <summary>MOD-0262 content id (the image itself never sits in the order).</summary>
    public Guid BinaryRef { get; set; }

    public string SignerName { get; set; } = string.Empty;
    public DateTimeOffset SignedAt { get; set; }
    public Guid CapturedByUserId { get; set; }
}

public sealed class PreOrderApproval
{
    public Guid? WorkflowInstanceId { get; set; }
    public int SubmissionNo { get; set; }
    public string? Decision { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? Reason { get; set; }
    public bool Auto { get; set; }

    /// <summary><c>work-center</c> | <c>email-link</c> | <c>auto</c>.</summary>
    public string? Channel { get; set; }
}

public sealed class PreOrderTransmission
{
    public List<PreOrderTransmissionAttempt> Attempts { get; set; } = new();
    public string LastStatus { get; set; } = string.Empty;
    public DateTimeOffset? TransmittedAt { get; set; }
    public Guid? DepotDocumentRef { get; set; }
    public Guid? PharmacyCopyRef { get; set; }

    /// <summary>WP-VW-W6 W6-BE-c1 — when the scheduler tries a failed transmission again (UTC ticks: one plain number, the
    /// CRM parallel-array trap); null = no automatic retry is due (sent, or the tenant's retry budget is spent — sales
    /// operations can still retry by hand).</summary>
    public long? NextAttemptAtTicks { get; set; }
}

public sealed class PreOrderTransmissionAttempt
{
    public DateTimeOffset At { get; set; }
    public List<string> DispatchIds { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? ByUserId { get; set; }
}

public sealed class PreOrderCancellation
{
    public Guid ByUserId { get; set; }
    public DateTimeOffset At { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool NotifiedDepot { get; set; }
    public bool NotifiedPharmacy { get; set; }
}

public sealed class PreOrderDepotResponse
{
    public string Status { get; set; } = string.Empty;
    public List<PreOrderPartialLine> PartialLines { get; set; } = new();
    public string? Note { get; set; }
    public Guid ByUserId { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class PreOrderPartialLine
{
    public int LineNo { get; set; }
    public int DeliveredQuantity { get; set; }
}

public sealed class PreOrderEvent
{
    public DateTimeOffset At { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? ByUserId { get; set; }

    /// <summary>A code + parameters (invariant text) — the client words it.</summary>
    public Dictionary<string, string?> Payload { get; set; } = new();
}

/// <summary>§12. Closed set; anything else is 400.</summary>
public static class PreOrderStatuses
{
    public const string Draft = "draft";
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Transmitted = "transmitted";
    public const string TransmissionFailed = "transmission_failed";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All =
        new[] { Draft, PendingApproval, Approved, Transmitted, TransmissionFailed, Rejected, Cancelled };
}

public static class PreOrderPriceModes
{
    public const string Priced = "priced";
    public const string Quantity = "quantity";
}

public static class PreOrderFulfilments
{
    public const string Depot = "depot";
}

public static class PreOrderDecisions
{
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string RequestChanges = "request-changes";

    public static readonly IReadOnlyList<string> All = new[] { Approve, Reject, RequestChanges };
}

public static class PreOrderApprovalModes
{
    public const string Always = "always";
    public const string AutoWithinLimits = "auto-within-limits";

    public static readonly IReadOnlyList<string> All = new[] { Always, AutoWithinLimits };
}

public static class PreOrderApprovalChannels
{
    public const string WorkCenter = "work-center";
    public const string EmailLink = "email-link";
    public const string Auto = "auto";
}

public static class PreOrderApprovalReasons
{
    public const string TenantSetting = "tenant-setting";
    public const string SoftExceeded = "soft-exceeded";
}

public static class PreOrderDepotResponseStatuses
{
    public const string Delivered = "delivered";
    public const string Partial = "partial";

    public static readonly IReadOnlyList<string> All = new[] { Delivered, Partial };
}

public static class PreOrderTransmissionStatuses
{
    public const string Sent = "sent";
    public const string Failed = "failed";
}

/// <summary>§12 — the event log row types.</summary>
public static class PreOrderEventTypes
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Discarded = "discarded";
    public const string SignatureCaptured = "signature-captured";
    public const string SignatureCleared = "signature-cleared";
    public const string Submitted = "submitted";
    public const string AutoApproved = "auto-approved";
    public const string ApprovalRequested = "approval-requested";
    public const string ApproverUnresolved = "approver-unresolved";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string ChangesRequested = "changes-requested";
    public const string TransmissionAttempted = "transmission-attempted";
    public const string Transmitted = "transmitted";
    public const string TransmissionFailed = "transmission-failed";
    public const string Cancelled = "cancelled";
    public const string DepotResponseRecorded = "depot-response-recorded";
    public const string CopiedFrom = "copied-from";
}
