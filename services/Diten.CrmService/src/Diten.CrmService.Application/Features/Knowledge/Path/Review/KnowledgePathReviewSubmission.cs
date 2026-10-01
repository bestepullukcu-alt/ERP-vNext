using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Review;

/// <summary>
/// WP-KP-2 (DESIGN-KP-STUDIO §3.2) — the submit gate and the frozen snapshot. Fail-closed, every refusal a 409 + code:
/// <c>chain_template_required</c> (legacy path), <c>invalid_status</c> (not a draft), <c>chain_conformance_failed</c>
/// (a slot under / over), <c>component_not_published</c>, <c>component_language_mismatch</c>,
/// <c>claim_no_country_version</c>. A claim's APPROVAL status is deliberately not a gate here — it may be approved during
/// the MLR review; the release (KP-3) is the hard gate. The open-round check is the handler's (it reconciles first).
/// </summary>
internal static class KnowledgePathReviewSubmission
{
    internal sealed record Gate(string Code, string Message)
    {
        public Common.Models.Response<T> To<T>() => KnowledgePathReviewRules.Fail<T>(Code, Message, 409);
    }

    public static Gate? CheckPath(KnowledgePath path)
    {
        if (path.ChainTemplate is null)
        {
            return new Gate(KnowledgePathStudioErrors.ChainTemplateRequired,
                "A legacy (chain-less) path cannot be sent for review; bind it to a chain first.");
        }

        return !string.Equals(path.PathStatus, KnowledgePathStatuses.Draft, StringComparison.Ordinal) || path.IsStepSetFrozen()
            ? new Gate(ClaimErrorCodes.InvalidStatus, $"Only a draft path can be sent for review (status: {path.PathStatus}).")
            : null;
    }

    /// <summary>Checks the content + claim gates and builds the frozen snapshot (null + gate on refusal).</summary>
    public static async Task<(KnowledgePathRevisionSnapshot? Snapshot, Gate? Gate)> FreezeAsync(
        Guid tenantId, KnowledgePath path, KnowledgePathStudioView studio,
        IKnowledgeContentRepository contents, IClaimRepository claims, CancellationToken ct)
    {
        var failing = studio.ChainConformance.Where(c => c.Status != KnowledgePathConformanceStatuses.Ok).ToList();
        if (studio.ChainTemplate is null || studio.ChainConformance.Count == 0)
        {
            return (null, new Gate(KnowledgePathStudioErrors.ChainTemplateInvalid,
                "The pinned chain template of this path can no longer be read."));
        }

        if (failing.Count > 0)
        {
            return (null, new Gate(KnowledgePathReviewErrors.ChainConformanceFailed,
                "The path does not fill its chain: " + string.Join(", ", failing.Select(c =>
                    $"{c.BranchCode}/{c.Name ?? c.ChainStepId.ToString("D")} {c.Count} ({c.Min}–{c.Max?.ToString() ?? "∞"}, {c.Status})"))));
        }

        var steps = new List<KnowledgePathRevisionStep>();
        foreach (var step in path.OrderedActiveSteps())
        {
            var content = await contents.GetByIdAsync(tenantId, step.ContentId, ct);
            if (content is null || content.IsArchived()
                || !string.Equals(content.ContentStatus, KnowledgeContentStatuses.Published, StringComparison.OrdinalIgnoreCase))
            {
                return (null, new Gate(KnowledgePathReviewErrors.ComponentNotPublished,
                    $"Step '{step.StepCode}' content '{content?.ContentCode ?? step.ContentCode}' is not published."));
            }

            if (!ChainContextValidation.SameLanguage(content.LanguageCode, path.LanguageCode))
            {
                return (null, new Gate(ChainContextErrors.ComponentLanguageMismatch,
                    $"Step '{step.StepCode}' content '{content.ContentCode}' is in '{content.LanguageCode}', not '{path.LanguageCode}'."));
            }

            steps.Add(new KnowledgePathRevisionStep
            {
                StepId = step.StepId, StepCode = step.StepCode, StepTitle = step.StepTitle, StepOrder = step.StepOrder,
                ContentId = content.Id, ContentCode = content.ContentCode, ContentVersion = content.ContentVersion,
                ContentLanguage = content.LanguageCode, IsRequired = step.IsRequired,
                EstimatedDurationMinutes = step.EstimatedDurationMinutes, PrerequisiteStepId = step.PrerequisiteStepId,
                Arrangement = step.Arrangement is null ? null : KnowledgePathStudio.Copy(step.Arrangement)
            });
        }

        var frozenClaims = new List<KnowledgePathRevisionClaim>();
        foreach (var read in studio.Claims)
        {
            if (read.CountryVersionId is null)
            {
                return (null, new Gate(KnowledgePathReviewErrors.ClaimNoCountryVersion,
                    $"Claim '{read.ClaimCode}' has no version in {path.CountryCode}."));
            }

            var claim = await claims.GetByIdAsync(tenantId, read.ClaimId, ct);
            frozenClaims.Add(new KnowledgePathRevisionClaim
            {
                ClaimId = read.ClaimId, ClaimCode = read.ClaimCode, ClaimVersion = claim?.ClaimVersion ?? string.Empty,
                CountryVersionId = read.CountryVersionId, CountryVersion = read.CountryVersion,
                CountryVersionStatus = read.Status,
                Arrangement = new KnowledgePathArrangement
                {
                    BranchCode = read.Arrangement.BranchCode, ChainStepId = read.Arrangement.ChainStepId,
                    Position = read.Arrangement.Position
                }
            });
        }

        var derived = studio.DerivedContext;
        return (new KnowledgePathRevisionSnapshot
        {
            ConceptChainTemplateId = path.ChainTemplate!.ConceptChainTemplateId,
            ChainVersion = path.ChainTemplate.ChainVersion,
            PathName = path.PathName,
            CountryCode = path.CountryCode,
            LanguageCode = path.LanguageCode,
            ProductId = derived?.ProductId,
            ProductCode = derived?.ProductCode,
            ProductName = derived?.ProductName,
            AudienceProfileIds = derived?.AudienceProfileIds.ToList() ?? new List<Guid>(),
            Steps = steps,
            Claims = frozenClaims,
            Conformance = studio.ChainConformance.Select(c => new KnowledgePathRevisionConformance
            {
                BranchCode = c.BranchCode, ChainStepId = c.ChainStepId, Count = c.Count, Min = c.Min, Max = c.Max,
                Status = c.Status
            }).ToList()
        }, null);
    }

    /// <summary>The difference to the previous revision: steps / claims added, removed, moved (slot or order) and
    /// content / claim version changes. Steps compare by StepId (stable inside a path version), claims by ClaimId.</summary>
    public static KnowledgePathChangeSummary Diff(KnowledgePathRevision? previous, KnowledgePathRevisionSnapshot current)
    {
        var summary = new KnowledgePathChangeSummary { ComparedToRevision = previous?.RevisionNumber };
        if (previous is null)
        {
            return summary;
        }

        var before = previous.Snapshot.Steps.ToDictionary(s => s.StepId);
        var after = current.Steps.ToDictionary(s => s.StepId);
        foreach (var step in current.Steps)
        {
            if (!before.TryGetValue(step.StepId, out var old))
            {
                summary.Items.Add(Change(KnowledgePathChangeKinds.StepAdded, step.StepCode, step.StepTitle, null, step.ContentCode));
                continue;
            }

            if (old.ContentId != step.ContentId)
            {
                summary.Items.Add(Change(KnowledgePathChangeKinds.ContentChanged, step.StepCode, step.StepTitle,
                    old.ContentCode, step.ContentCode));
            }
            else if (!string.Equals(old.ContentVersion, step.ContentVersion, StringComparison.Ordinal))
            {
                summary.Items.Add(Change(KnowledgePathChangeKinds.ContentVersionChanged, step.StepCode, step.StepTitle,
                    old.ContentVersion, step.ContentVersion));
            }

            // Moved = another slot or another position inside it (a StepOrder shift caused by OTHER steps is not a move).
            if (Slot(old.Arrangement) != Slot(step.Arrangement) || old.Arrangement?.Position != step.Arrangement?.Position)
            {
                summary.Items.Add(Change(KnowledgePathChangeKinds.StepMoved, step.StepCode, step.StepTitle,
                    $"{Slot(old.Arrangement)}#{old.Arrangement?.Position}", $"{Slot(step.Arrangement)}#{step.Arrangement?.Position}"));
            }
        }

        foreach (var removed in previous.Snapshot.Steps.Where(s => !after.ContainsKey(s.StepId)))
        {
            summary.Items.Add(Change(KnowledgePathChangeKinds.StepRemoved, removed.StepCode, removed.StepTitle, removed.ContentCode, null));
        }

        var claimsBefore = previous.Snapshot.Claims.ToDictionary(c => c.ClaimId);
        var claimsAfter = current.Claims.ToDictionary(c => c.ClaimId);
        foreach (var claim in current.Claims)
        {
            if (!claimsBefore.TryGetValue(claim.ClaimId, out var old))
            {
                summary.Items.Add(Change(KnowledgePathChangeKinds.ClaimAdded, claim.ClaimCode, null, null, claim.CountryVersion));
                continue;
            }

            if (old.CountryVersionId != claim.CountryVersionId
                || !string.Equals(old.ClaimVersion, claim.ClaimVersion, StringComparison.Ordinal))
            {
                summary.Items.Add(Change(KnowledgePathChangeKinds.ClaimVersionChanged, claim.ClaimCode, null,
                    $"{old.ClaimVersion}/{old.CountryVersion}", $"{claim.ClaimVersion}/{claim.CountryVersion}"));
            }

            if (Slot(old.Arrangement) != Slot(claim.Arrangement) || old.Arrangement.Position != claim.Arrangement.Position)
            {
                summary.Items.Add(Change(KnowledgePathChangeKinds.ClaimMoved, claim.ClaimCode, null,
                    $"{Slot(old.Arrangement)}#{old.Arrangement.Position}", $"{Slot(claim.Arrangement)}#{claim.Arrangement.Position}"));
            }
        }

        foreach (var removed in previous.Snapshot.Claims.Where(c => !claimsAfter.ContainsKey(c.ClaimId)))
        {
            summary.Items.Add(Change(KnowledgePathChangeKinds.ClaimRemoved, removed.ClaimCode, null, removed.CountryVersion, null));
        }

        return summary;
    }

    /// <summary>The previous revision's UNRESOLVED notes, carried with their origin (Öneri 3).</summary>
    public static List<KnowledgePathRevisionNote> CarryNotes(KnowledgePathRevision? previous)
        => previous?.Notes.Where(n => !n.IsResolved()).Select(n => new KnowledgePathRevisionNote
        {
            PageRef = n.PageRef, BlockRef = n.BlockRef, StepRef = n.StepRef, X = n.X, Y = n.Y, Text = n.Text,
            Author = n.Author, CreatedAt = n.CreatedAt, CarriedFromRevision = n.CarriedFromRevision ?? previous.RevisionNumber
        }).ToList() ?? new List<KnowledgePathRevisionNote>();

    private static string Slot(KnowledgePathArrangement? a)
        => a is null ? "-" : $"{a.BranchCode.ToUpperInvariant()}/{a.ChainStepId:D}";

    private static KnowledgePathChange Change(string kind, string reference, string? label, string? from, string? to)
        => new() { Kind = kind, Ref = reference, Label = label, From = from, To = to };
}
