using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;

namespace Diten.ManagementGovernanceService.Domain.DecisionRegistry;

public static class DecisionRegistryContracts
{
    public const string DecisionReference = "management-governance.decision-reference";
    public const string AuthorityReference = "management-governance.decision-authority-reference";
    public const string Version = "1.0";
}

public enum DecisionLifecycleState { Unpublished, Published, Withdrawn, Superseded }
public enum DecisionAuthorityKind { Person, Position }
public enum DecisionReferenceValidationMode { HistoricalResolve, NewReferenceEligibility, CurrentSelectionEligibility }
public enum DecisionDisposition { Published, Withdrawn, Superseded }

public sealed record DecisionRevisionReferenceV1
{
    public DecisionRevisionReferenceV1(Guid decisionId, Guid revisionId, int revisionNumber)
    {
        DecisionId = Guard.Id(decisionId);
        DecisionRevisionId = Guard.Id(revisionId);
        DecisionRevisionNumber = revisionNumber > 0 ? revisionNumber : throw new DecisionRegistryDomainException("Revision number must be positive.");
    }
    public string ContractName => DecisionRegistryContracts.DecisionReference;
    public string ContractVersion => DecisionRegistryContracts.Version;
    public Guid DecisionId { get; }
    public Guid DecisionRevisionId { get; }
    public int DecisionRevisionNumber { get; }
}

public sealed record DecisionAuthorityReferenceV1
{
    public DecisionAuthorityReferenceV1(DecisionAuthorityKind kind, Guid id) { AuthorityKind = kind; AuthorityId = Guard.Id(id); }
    public string ContractName => DecisionRegistryContracts.AuthorityReference;
    public string ContractVersion => DecisionRegistryContracts.Version;
    public DecisionAuthorityKind AuthorityKind { get; }
    public Guid AuthorityId { get; }
}

public sealed record TypedReference
{
    public TypedReference(string contractName, string contractVersion, Guid referenceId)
    { ContractName = Guard.Text(contractName, 200); ContractVersion = Guard.Text(contractVersion, 32); ReferenceId = Guard.Id(referenceId); }
    public string ContractName { get; }
    public string ContractVersion { get; }
    public Guid ReferenceId { get; }
}

public sealed class DecisionRecord : TenantScopedEntity
{
    [SetsRequiredMembers]
    private DecisionRecord(Guid tenant, Guid id, string subjectKind, Guid subjectId, Guid actor)
    { TenantId = Guard.Id(tenant); Id = DecisionId = Guard.Id(id); SubjectKind = Guard.Text(subjectKind, 128); SubjectId = Guard.Id(subjectId); CreatedBy = Guard.Id(actor).ToString("D"); }
    public Guid DecisionId { get; private init; }
    public string SubjectKind { get; private init; } = string.Empty;
    public Guid SubjectId { get; private init; }
    public DecisionLifecycleState LifecycleState { get; private set; }
    public Guid? CurrentRevisionId { get; private set; }
    public int? CurrentRevisionNumber { get; private set; }
    public static DecisionRecord Create(Guid tenant, Guid id, string kind, Guid subject, Guid actor) => new(tenant, id, kind, subject, actor);
    public void Publish(Guid revision, int number, int expectedVersion)
    { Match(expectedVersion); if (LifecycleState is DecisionLifecycleState.Withdrawn or DecisionLifecycleState.Superseded) Fail("Terminal decision cannot reopen."); if (number != (CurrentRevisionNumber ?? 0) + 1) Fail("Revision number must be monotonic."); CurrentRevisionId = Guard.Id(revision); CurrentRevisionNumber = number; LifecycleState = DecisionLifecycleState.Published; Version++; }
    public void Withdraw(int expectedVersion) { RequirePublished(expectedVersion); LifecycleState = DecisionLifecycleState.Withdrawn; Version++; }
    public void Supersede(Guid target, int expectedVersion) { RequirePublished(expectedVersion); if (Guard.Id(target) == DecisionId) Fail("Decision cannot supersede itself."); LifecycleState = DecisionLifecycleState.Superseded; Version++; }
    private void RequirePublished(int version) { Match(version); if (LifecycleState != DecisionLifecycleState.Published || CurrentRevisionId is null) Fail("Published decision required."); }
    private void Match(int version) { if (Version != version) Fail("Concurrency conflict."); }
    private static void Fail(string value) => throw new DecisionRegistryDomainException(value);
}

public sealed class DecisionDraft : TenantScopedEntity
{
    [SetsRequiredMembers]
    private DecisionDraft(Guid tenant, Guid id, Guid decision, string text, string result, string rationale, DecisionAuthorityReferenceV1 authority, TypedReference provenance, Guid actor)
    { TenantId = Guard.Id(tenant); Id = DraftId = Guard.Id(id); DecisionId = Guard.Id(decision); CreatedByActorId = LatestUpdatedByActorId = Guard.Id(actor); CreatedBy = actor.ToString("D"); Set(text, result, rationale, authority, provenance); }
    public Guid DraftId { get; private init; }
    public Guid DecisionId { get; private init; }
    public string DecisionText { get; private set; } = string.Empty;
    public string DecisionResult { get; private set; } = string.Empty;
    public string Rationale { get; private set; } = string.Empty;
    public DecisionAuthorityReferenceV1 Authority { get; private set; } = null!;
    public TypedReference Provenance { get; private set; } = null!;
    public IReadOnlyList<TypedReference> EvidenceReferences => Array.Empty<TypedReference>();
    public Guid CreatedByActorId { get; private init; }
    public Guid LatestUpdatedByActorId { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public static DecisionDraft Create(Guid tenant, Guid id, Guid decision, string text, string result, string rationale, DecisionAuthorityReferenceV1 authority, TypedReference provenance, Guid actor) => new(tenant, id, decision, text, result, rationale, authority, provenance, actor);
    public void Revise(string text, string result, string rationale, DecisionAuthorityReferenceV1 authority, TypedReference provenance, int expected, Guid actor) { Mutable(expected); Set(text, result, rationale, authority, provenance); LatestUpdatedByActorId = Guard.Id(actor); Version++; }
    public void SoftDelete(int expected, DateTimeOffset now, bool legalHold, bool hasHistory) { Mutable(expected); if (legalHold || hasHistory) throw new DecisionRegistryDomainException("Draft deletion denied."); IsDeleted = true; DeletedAt = Guard.Utc(now); Version++; }
    public void RequirePublisherSeparation(Guid publisher) { if (publisher == CreatedByActorId || publisher == LatestUpdatedByActorId) throw new DecisionRegistryDomainException("Publisher separation violated."); }
    private void Mutable(int expected) { if (IsDeleted || Version != expected) throw new DecisionRegistryDomainException("Draft is not mutable."); }
    private void Set(string text, string result, string rationale, DecisionAuthorityReferenceV1 authority, TypedReference provenance) { DecisionText = Guard.Text(text, 8000); DecisionResult = Guard.Text(result, 4000); Rationale = Guard.Text(rationale, 12000); Authority = authority ?? throw new DecisionRegistryDomainException("Authority required."); Provenance = provenance ?? throw new DecisionRegistryDomainException("Provenance required."); }
}

public sealed class DecisionRevision : TenantScopedEntity
{
    [SetsRequiredMembers]
    private DecisionRevision(Guid tenant, Guid revision, Guid decision, int number, string text, string result, string rationale, DecisionAuthorityReferenceV1 authority, TypedReference provenance, Guid recorder, DateTimeOffset recorded, Guid publisher, DateTimeOffset published)
    { TenantId = Guard.Id(tenant); Id = DecisionRevisionId = Guard.Id(revision); DecisionId = Guard.Id(decision); DecisionRevisionNumber = number > 0 ? number : throw new DecisionRegistryDomainException("Revision number must be positive."); DecisionText = Guard.Text(text, 8000); DecisionResult = Guard.Text(result, 4000); Rationale = Guard.Text(rationale, 12000); Authority = authority; Provenance = provenance; RecordedBy = Guard.Id(recorder); RecordedAtUtc = Guard.Utc(recorded); PublishedBy = Guard.Id(publisher); PublishedAtUtc = Guard.Utc(published); CreatedBy = recorder.ToString("D"); }
    public Guid DecisionRevisionId { get; private init; }
    public Guid DecisionId { get; private init; }
    public int DecisionRevisionNumber { get; private init; }
    public string DecisionText { get; private init; } = string.Empty;
    public string DecisionResult { get; private init; } = string.Empty;
    public string Rationale { get; private init; } = string.Empty;
    public DecisionAuthorityReferenceV1 Authority { get; private init; } = null!;
    public TypedReference Provenance { get; private init; } = null!;
    public IReadOnlyList<TypedReference> EvidenceReferences => Array.Empty<TypedReference>();
    public Guid RecordedBy { get; private init; }
    public DateTimeOffset RecordedAtUtc { get; private init; }
    public Guid PublishedBy { get; private init; }
    public DateTimeOffset PublishedAtUtc { get; private init; }
    public static DecisionRevision Publish(Guid tenant, Guid revision, Guid decision, int number, string text, string result, string rationale, DecisionAuthorityReferenceV1 authority, TypedReference provenance, Guid recorder, DateTimeOffset recorded, Guid publisher, DateTimeOffset published) => new(tenant, revision, decision, number, text, result, rationale, authority, provenance, recorder, recorded, publisher, published);
}

public sealed record DecisionPublicationHistory(Guid TenantId, Guid PublicationHistoryId, Guid DecisionId, Guid DecisionRevisionId, DecisionDisposition Disposition, TypedReference Reason, DateTimeOffset OccurredAtUtc);
public sealed record DecisionRevisionReplacementProvenance(Guid TenantId, Guid DecisionId, Guid PriorDecisionRevisionId, Guid NextDecisionRevisionId, int NextDecisionRevisionNumber);
public sealed record DecisionSupersessionLink(Guid TenantId, Guid SourceDecisionId, Guid SourceDecisionRevisionId, Guid TargetDecisionId, Guid TargetDecisionRevisionId, TypedReference Reason, DateTimeOffset EffectiveAtUtc)
{
    public static DecisionSupersessionLink Create(Guid tenant, Guid sourceDecision, Guid sourceRevision, Guid targetDecision, Guid targetRevision, TypedReference reason, DateTimeOffset effective)
    { if (Guard.Id(sourceDecision) == Guard.Id(targetDecision)) throw new DecisionRegistryDomainException("Supersession decisions must differ."); return new(Guard.Id(tenant), sourceDecision, Guard.Id(sourceRevision), targetDecision, Guard.Id(targetRevision), reason, Guard.Utc(effective)); }
}

public sealed class DecisionRegistryDomainException(string message) : InvalidOperationException(message);
internal static class Guard
{
    public static Guid Id(Guid value) => value == Guid.Empty ? throw new DecisionRegistryDomainException("Identifier cannot be empty.") : value;
    public static string Text(string? value, int max) { var normalized = value?.Trim().Normalize(); return string.IsNullOrEmpty(normalized) || normalized.Length > max ? throw new DecisionRegistryDomainException("Required text is invalid.") : normalized; }
    public static DateTimeOffset Utc(DateTimeOffset value) => value.Offset != TimeSpan.Zero ? throw new DecisionRegistryDomainException("UTC required.") : value;
}
