using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Services;

/// <summary>
/// MOD-0029-FU15 — determines which active legal holds reach a given regulated record (GMG-QMS-SOP-0001 §22).
/// A litigation hold stops ALL destruction activity within its scope, so this evaluator is the single gate every
/// eligibility and disposition decision consults.
///
/// SCOPE SEMANTICS:
/// • GlobalDocumentControl — reaches every document governance record in the tenant.
/// • RegisterEntry — reaches the register entry itself and every subject linked to it (its versions, approval
///   evidence, gate evidence, reviews, copies …), which is why subjects carry RegisterEntryId.
/// • ControlledDocument — reaches the controlled document and its subjects.
/// • SubjectType — reaches every record of the listed subject types.
/// • ExternalDocument — reaches the external register entry and its monitoring checks, impact assessments and
///   internal links.
/// • Repository — reaches subjects explicitly enrolled via DocumentLegalHoldSubject membership.
/// • CustomQuery — NOT evaluated in this FU. The scope is stored as a description; it never blocks silently and
///   never expands implicitly. Executing custom scope queries is a future task.
///
/// Explicit DocumentLegalHoldSubject membership ALWAYS blocks, whatever the scope type — that is the evidence
/// that the hold actually reached the record.
/// </summary>
public sealed class DocumentLegalHoldEvaluator
{
    private readonly IDocumentLegalHoldRepository _holds;
    private readonly IDocumentLegalHoldSubjectRepository _holdSubjects;

    public DocumentLegalHoldEvaluator(
        IDocumentLegalHoldRepository holds,
        IDocumentLegalHoldSubjectRepository holdSubjects)
    {
        _holds = holds;
        _holdSubjects = holdSubjects;
    }

    /// <summary>The active holds blocking this subject. Empty means no hold — never "unknown".</summary>
    public async Task<IReadOnlyList<DocumentLegalHold>> GetBlockingHoldsAsync(
        RetentionSubjectType subjectType,
        Guid subjectId,
        Guid? registerEntryId,
        Guid? controlledDocumentId,
        DateTimeOffset at,
        CancellationToken ct)
    {
        var active = (await _holds.GetActiveAsync(ct)).Where(h => h.IsActiveAt(at)).ToList();
        if (active.Count == 0)
        {
            return [];
        }

        // Explicit membership is the strongest signal and is scope-type independent.
        var membershipHoldIds = (await _holdSubjects.GetBySubjectAsync(subjectType, subjectId, ct))
            .Where(m => m.Status == LegalHoldSubjectStatus.Active)
            .Select(m => m.LegalHoldId)
            .ToHashSet();

        return active
            .Where(h => membershipHoldIds.Contains(h.Id) || ReachesByScope(h, subjectType, subjectId, registerEntryId, controlledDocumentId))
            .ToList();
    }

    private static bool ReachesByScope(
        DocumentLegalHold hold,
        RetentionSubjectType subjectType,
        Guid subjectId,
        Guid? registerEntryId,
        Guid? controlledDocumentId) => hold.ScopeType switch
        {
            LegalHoldScopeType.GlobalDocumentControl => true,

            LegalHoldScopeType.RegisterEntry =>
                (registerEntryId is { } entryId && hold.RegisterEntryIds.Contains(entryId))
                || (subjectType == RetentionSubjectType.DocumentMasterRegisterEntry && hold.RegisterEntryIds.Contains(subjectId)),

            LegalHoldScopeType.ControlledDocument =>
                (controlledDocumentId is { } docId && hold.ControlledDocumentIds.Contains(docId))
                || (subjectType == RetentionSubjectType.ControlledDocument && hold.ControlledDocumentIds.Contains(subjectId)),

            LegalHoldScopeType.SubjectType => hold.SubjectTypes.Contains(subjectType),

            LegalHoldScopeType.ExternalDocument => IsExternalSubject(subjectType)
                && (hold.ExternalDocumentIds.Contains(subjectId)
                    || (registerEntryId is { } externalId && hold.ExternalDocumentIds.Contains(externalId))),

            // Membership-driven only — handled by the caller above.
            LegalHoldScopeType.Repository => false,

            // Deliberately not evaluated in FU15. Never blocks implicitly.
            LegalHoldScopeType.CustomQuery => false,

            _ => false
        };

    private static bool IsExternalSubject(RetentionSubjectType t) =>
        t is RetentionSubjectType.ExternalDocumentRegisterEntry
            or RetentionSubjectType.ExternalDocumentMonitoringCheck
            or RetentionSubjectType.ExternalDocumentImpactAssessment
            or RetentionSubjectType.ExternalDocumentInternalLink;
}
