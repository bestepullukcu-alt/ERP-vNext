using Diten.CrmService.Application.Common.Artifacts;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Release;

/// <summary>
/// WP-KP-3 (DESIGN-KP-STUDIO §3.4) — renders an APPROVED knowledge path revision into one PDF. Deterministic over the
/// prepared <see cref="KnowledgePathRenderModel"/> (built from the frozen snapshot + names + the MLR round): no I/O, no
/// storage — bytes in, bytes out (a PDF begins with <c>%PDF</c>). HTML output is SB-4.
/// </summary>
public interface IKnowledgePathRevisionRenderer
{
    RenderedContent Render(KnowledgePathRenderModel model);
}

/// <summary>Everything the PDF shows, already resolved to display text (names, the path-language claim text).</summary>
public sealed record KnowledgePathRenderModel(
    string ApprovalCode,
    string PathCode,
    string PathName,
    string PathVersion,
    int RevisionNumber,
    string? ChainName,
    string? ChainVersion,
    string? CountryName,
    string? LanguageName,
    string? ProductName,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<KnowledgePathRenderSlot> Slots,
    IReadOnlyList<KnowledgePathRenderReviewEntry> Review,
    string? SubmittedBy,
    DateTimeOffset GeneratedAt,
    string? GeneratedBy);

/// <summary>One chain step (slot), in branch-first order: its contents (field order) and its claims.</summary>
public sealed record KnowledgePathRenderSlot(
    string BranchLabel,
    string SlotLabel,
    int Count,
    int Min,
    int? Max,
    string Status,
    IReadOnlyList<KnowledgePathRenderContent> Contents,
    IReadOnlyList<KnowledgePathRenderClaim> Claims);

public sealed record KnowledgePathRenderContent(int Order, string Title, string ContentCode, string ContentVersion, bool IsRequired);

/// <summary>A claim on a slot: code, the country version's text and qualifier in the path language, the version.</summary>
public sealed record KnowledgePathRenderClaim(string ClaimCode, string? Text, string? Qualifier, string? CountryVersion);

/// <summary>One MLR transition: step name, decision, person, time, comment.</summary>
public sealed record KnowledgePathRenderReviewEntry(string Action, string? StepName, string? Actor, DateTimeOffset At, string? Comment);
