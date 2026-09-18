namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// SCMM-14 (CAND-CAP-0011, docx §7 / §11, DESIGN D14-a) — a reusable, versioned CONTEXT/SCOPE: where a piece of content
/// applies. Product / market / audience references (opaque config strings, sector-neutral) plus optional channel,
/// language and an effective period. A <see cref="Diten.CrmService.Domain.Entities.ContentSet"/> references a scope by
/// id + version (pinned), never inline — "define once, reference many". This is NOT MOD-0167 StrategyTemplate (that is a
/// separate, shipped play-binding aggregate); ContentScope carries no frequency, SKU mix or content binding.
/// <para>
/// <see cref="EntityBase.Id"/> is the ScopeId and <see cref="ScopeCode"/> is the stable business key; the business
/// version is <see cref="ScopeVersion"/> (never <see cref="EntityBase.Version"/>, the concurrency token). Closing a
/// scope is the soft <see cref="ArchivedAt"/> lifecycle; there is no hard delete. Legal-entity is deliberately NOT a
/// dimension (DESIGN LE) — the market axis is sufficient and eligibility carries no LE.
/// </para>
/// </summary>
public sealed class ContentScope : EntityBase
{
    /// <summary>Stable business key, shared across the versions of one logical scope, unique per tenant among
    /// non-archived rows.</summary>
    public string ScopeCode { get; set; } = string.Empty;

    public string ScopeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Product references (opaque config strings — MDM ids or codes; nothing is resolved or copied here).</summary>
    public List<string> ProductRefs { get; set; } = new();

    /// <summary>Market references (opaque config strings; the market axis subsumes legal-entity — DESIGN LE).</summary>
    public List<string> MarketRefs { get; set; } = new();

    /// <summary>Audience references (opaque config strings; audience-profile ids or dimension values).</summary>
    public List<string> AudienceRefs { get; set; } = new();

    /// <summary>Optional single channel (opaque config string).</summary>
    public string? Channel { get; set; }

    /// <summary>Optional language the scope targets.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>Optional usage period (open-ended when either bound is null).</summary>
    public DateTimeOffset? PeriodFrom { get; set; }
    public DateTimeOffset? PeriodTo { get; set; }

    /// <summary>Business version (NOT <see cref="EntityBase.Version"/>, the concurrency token).</summary>
    public string ScopeVersion { get; set; } = string.Empty;

    /// <summary><see cref="ContentScopeStatuses"/> — draft / active / archived.</summary>
    public string Status { get; set; } = ContentScopeStatuses.Draft;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}

/// <summary>Content-scope lifecycle. Hard delete does not exist; closing a scope is archive. In-domain (structural).</summary>
public static class ContentScopeStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>Canonical SCMM-14 content-scope reason / outcome codes surfaced on write outcomes and audit.</summary>
public static class ContentScopeReasonCodes
{
    public const string Created = "content_scope_created";
    public const string Updated = "content_scope_updated";
    public const string Archived = "content_scope_archived";
    public const string DuplicateCode = "content_scope_duplicate_code";
}
