namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §4.4 (D3, D4): the one-time approval link of one submission. Only the SHA-256 of the
/// token is stored (the plain token goes in the e-mail only). The link SIGNS THE USER IN — there is no decision without a
/// session; it is single-use, expires after the tenant's TTL and dies when the order leaves that submission.
/// </summary>
public sealed class PreOrderApprovalLink : EntityBase
{
    public Guid PreOrderId { get; set; }

    /// <summary>The submission the link belongs to (<see cref="PreOrder.SubmissionNo"/>).</summary>
    public int SubmissionNo { get; set; }

    /// <summary>Lowercase hex SHA-256 of the token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public List<Guid> ApproverUserIds { get; set; } = new();
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public Guid? ConsumedByUserId { get; set; }

    /// <summary>Set when the order left the submission (decided elsewhere, cancelled, sent back) — the link is void.</summary>
    public DateTimeOffset? SupersededAt { get; set; }
}

/// <summary>The states the link screen shows (AC-LINK-2).</summary>
public static class PreOrderApprovalLinkStates
{
    public const string Valid = "valid";
    public const string Expired = "expired";
    public const string Consumed = "consumed";
    public const string Superseded = "superseded";
}

/// <summary>
/// RCS-001 §5.8 — the ORD counter of one (tenant, company prefix): advanced atomically (<c>findOneAndUpdate</c> +
/// <c>$inc</c>), never reset, a spent number is never given back.
/// </summary>
public sealed class PreOrderCodeSequence : EntityBase
{
    public string Prefix { get; set; } = string.Empty;
    public long Current { get; set; }
}
