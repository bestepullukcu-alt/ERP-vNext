using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>
/// SCMM-11 (CAND-CAP-0011, docx C2) — the SINGLE source of truth for an eligibility decision. Both consumers are thin
/// adapters over this one query: the in-process <see cref="IEligibilityEvaluationPort"/> and, later, the HTTP action
/// (SCMM-11 follow). It resolves ONE <see cref="EligibilityContext"/> against ONE policy (by id) at an instant and
/// returns a disjoint <see cref="EligibilityResult"/>. No side effect on the domain (read); it writes only the RM4
/// evaluation log. <paramref name="PinnedSelections"/> are the content/component selections under consideration — echoed
/// into the log, not evaluated by conditions in this slice. <paramref name="At"/> defaults to now (server-resolved).
/// </summary>
public sealed record ResolveEligibilityQuery(
    Guid PolicyId,
    EligibilityContext Context,
    IReadOnlyList<string>? PinnedSelections = null,
    DateTimeOffset? At = null) : IRequest<Response<EligibilityResult>>;
