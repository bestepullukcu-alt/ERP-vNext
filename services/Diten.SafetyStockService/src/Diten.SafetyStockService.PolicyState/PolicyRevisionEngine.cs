namespace Diten.SafetyStockService.PolicyState;

public sealed class PolicyRevisionEngine
{
    public PolicyRevisionResult CreateDraft(
        string revisionId,
        string policyId,
        PolicyScope? scope,
        string preparedBy,
        string contentKey)
    {
        if (string.IsNullOrWhiteSpace(revisionId)
            || string.IsNullOrWhiteSpace(policyId)
            || string.IsNullOrWhiteSpace(preparedBy)
            || string.IsNullOrWhiteSpace(contentKey)
            || !HasCompleteScope(scope))
        {
            return Fail(PolicyRevisionError.InvalidInput, "A revision, policy, scope, preparer, and content key are required.");
        }

        return PolicyRevisionResult.Success(new PolicyRevision(
            revisionId,
            policyId,
            scope!,
            null,
            contentKey,
            PolicyRevisionState.Draft,
            1,
            preparedBy,
            [preparedBy],
            [revisionId],
            null));
    }

    public PolicyRevisionResult ReviseMaterial(
        PolicyRevision? source,
        long expectedVersion,
        string newRevisionId,
        string editorId,
        string newContentKey)
    {
        if (source is null
            || string.IsNullOrWhiteSpace(newRevisionId)
            || string.IsNullOrWhiteSpace(editorId)
            || string.IsNullOrWhiteSpace(newContentKey))
        {
            return Fail(PolicyRevisionError.InvalidInput, "A source revision, new identity, editor, and content key are required.");
        }

        if (HasVersionConflict(source, expectedVersion))
            return Fail(PolicyRevisionError.VersionConflict, "The expected revision version is stale.");

        if (source.HasRevisionId(newRevisionId))
            return Fail(PolicyRevisionError.DuplicateRevisionId, "The new revision identity already exists in this lineage.");

        if (string.Equals(source.ContentKey, newContentKey, StringComparison.Ordinal))
            return Fail(PolicyRevisionError.UnchangedContent, "A material revision requires a different content key.");

        return PolicyRevisionResult.Success(CreateSuccessor(source, newRevisionId, editorId, newContentKey));
    }

    public PolicyRevisionResult RetryRejected(
        PolicyRevision? source,
        long expectedVersion,
        string newRevisionId,
        string preparedBy)
    {
        if (source is null || string.IsNullOrWhiteSpace(newRevisionId) || string.IsNullOrWhiteSpace(preparedBy))
            return Fail(PolicyRevisionError.InvalidInput, "A rejected revision, new identity, and preparer are required.");

        if (HasVersionConflict(source, expectedVersion))
            return Fail(PolicyRevisionError.VersionConflict, "The expected revision version is stale.");

        if (source.State != PolicyRevisionState.Rejected)
            return Fail(PolicyRevisionError.InvalidTransition, "Only a rejected revision can be retried.");

        if (source.HasRevisionId(newRevisionId))
            return Fail(PolicyRevisionError.DuplicateRevisionId, "The new revision identity already exists in this lineage.");

        return PolicyRevisionResult.Success(CreateSuccessor(source, newRevisionId, preparedBy, source.ContentKey));
    }

    public PolicyRevisionResult SubmitForReview(PolicyRevision? revision, long expectedVersion)
    {
        if (revision is null)
            return Fail(PolicyRevisionError.InvalidInput, "A revision is required.");

        if (HasVersionConflict(revision, expectedVersion))
            return Fail(PolicyRevisionError.VersionConflict, "The expected revision version is stale.");

        if (revision.State != PolicyRevisionState.Draft)
            return Fail(PolicyRevisionError.InvalidTransition, "Only a draft revision can be submitted for review.");

        return PolicyRevisionResult.Success(Copy(revision, PolicyRevisionState.InReview, null));
    }

    public PolicyRevisionResult Approve(PolicyRevision? revision, long expectedVersion, string approverId)
    {
        if (revision is null || string.IsNullOrWhiteSpace(approverId))
            return Fail(PolicyRevisionError.InvalidInput, "A revision and approver are required.");

        if (HasVersionConflict(revision, expectedVersion))
            return Fail(PolicyRevisionError.VersionConflict, "The expected revision version is stale.");

        if (revision.State != PolicyRevisionState.InReview)
            return Fail(PolicyRevisionError.InvalidTransition, "Only a revision in review can be approved.");

        if (revision.MaterialContributors.Contains(approverId, StringComparer.Ordinal))
            return Fail(PolicyRevisionError.SelfApproval, "A preparer or material editor cannot approve their own revision.");

        var decision = new RevisionDecision(approverId, PolicyRevisionState.Approved, null, revision.Version + 1);
        return PolicyRevisionResult.Success(Copy(revision, PolicyRevisionState.Approved, decision));
    }

    public PolicyRevisionResult Reject(PolicyRevision? revision, long expectedVersion, string reviewerId, string? reason)
    {
        if (revision is null || string.IsNullOrWhiteSpace(reviewerId))
            return Fail(PolicyRevisionError.InvalidInput, "A revision and reviewer are required.");

        if (HasVersionConflict(revision, expectedVersion))
            return Fail(PolicyRevisionError.VersionConflict, "The expected revision version is stale.");

        if (revision.State != PolicyRevisionState.InReview)
            return Fail(PolicyRevisionError.InvalidTransition, "Only a revision in review can be rejected.");

        if (string.IsNullOrWhiteSpace(reason))
            return Fail(PolicyRevisionError.MissingRejectionReason, "A rejection reason is required.");

        var decision = new RevisionDecision(reviewerId, PolicyRevisionState.Rejected, reason, revision.Version + 1);
        return PolicyRevisionResult.Success(Copy(revision, PolicyRevisionState.Rejected, decision));
    }

    private static PolicyRevision CreateSuccessor(
        PolicyRevision source,
        string newRevisionId,
        string preparedBy,
        string contentKey)
    {
        var contributors = source.MaterialContributors
            .Append(preparedBy)
            .Distinct(StringComparer.Ordinal);

        return new PolicyRevision(
            newRevisionId,
            source.PolicyId,
            source.Scope,
            source.Id,
            contentKey,
            PolicyRevisionState.Draft,
            1,
            preparedBy,
            contributors,
            source.LineageRevisionIds.Append(newRevisionId),
            null);
    }

    private static PolicyRevision Copy(
        PolicyRevision source,
        PolicyRevisionState state,
        RevisionDecision? decision) =>
        new(
            source.Id,
            source.PolicyId,
            source.Scope,
            source.PreviousRevisionId,
            source.ContentKey,
            state,
            source.Version + 1,
            source.PreparedBy,
            source.MaterialContributors,
            source.LineageRevisionIds,
            decision);

    private static bool HasCompleteScope(PolicyScope? scope) =>
        scope is not null
        && !string.IsNullOrWhiteSpace(scope.TenantId)
        && !string.IsNullOrWhiteSpace(scope.LegalEntityId)
        && !string.IsNullOrWhiteSpace(scope.SkuId)
        && !string.IsNullOrWhiteSpace(scope.WarehouseId);

    private static bool HasVersionConflict(PolicyRevision revision, long expectedVersion) =>
        revision.Version != expectedVersion || revision.Version == long.MaxValue;

    private static PolicyRevisionResult Fail(PolicyRevisionError error, string detail) =>
        PolicyRevisionResult.Failure(error, detail);
}
