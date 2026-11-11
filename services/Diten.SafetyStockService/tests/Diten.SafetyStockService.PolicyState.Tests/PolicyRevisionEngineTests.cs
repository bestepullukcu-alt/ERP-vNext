using Diten.SafetyStockService.PolicyState;
using Xunit;

namespace Diten.SafetyStockService.PolicyState.Tests;

public sealed class PolicyRevisionEngineTests
{
    private static readonly PolicyScope Scope = new("tenant-1", "legal-1", "sku-1", "warehouse-1");
    private readonly PolicyRevisionEngine _engine = new();

    [Fact]
    public void CreateDraft_CompleteSyntheticScope_ReturnsDraft()
    {
        // Arrange
        var scope = Scope;

        // Act
        var result = _engine.CreateDraft("revision-1", "policy-1", scope, "planner", "content-1");

        // Assert
        var revision = Assert.IsType<PolicyRevision>(result.Revision);
        Assert.Equal(PolicyRevisionState.Draft, revision.State);
        Assert.Equal(1, revision.Version);
        Assert.Equal("policy-1", revision.PolicyId);
        Assert.Equal(scope, revision.Scope);
        Assert.Null(revision.Decision);
    }

    [Fact]
    public void CreateDraft_MissingScopePart_ReturnsExplicitError()
    {
        // Arrange
        var scope = Scope with { LegalEntityId = "" };

        // Act
        var result = _engine.CreateDraft("revision-1", "policy-1", scope, "planner", "content-1");

        // Assert
        Assert.Equal(PolicyRevisionError.InvalidInput, result.Error);
        Assert.Null(result.Revision);
    }

    [Fact]
    public void SubmitForReview_Draft_ReturnsNewVersionWithoutChangingSource()
    {
        // Arrange
        var draft = Draft();

        // Act
        var result = _engine.SubmitForReview(draft, draft.Version);

        // Assert
        var review = Assert.IsType<PolicyRevision>(result.Revision);
        Assert.Equal(PolicyRevisionState.InReview, review.State);
        Assert.Equal(2, review.Version);
        Assert.Equal(PolicyRevisionState.Draft, draft.State);
        Assert.Equal(1, draft.Version);
    }

    [Fact]
    public void Reject_InReviewWithReason_ReturnsVisibleMemoryDecision()
    {
        // Arrange
        var review = InReview();

        // Act
        var result = _engine.Reject(review, review.Version, "reviewer", "Demand evidence is insufficient.");

        // Assert
        var rejected = Assert.IsType<PolicyRevision>(result.Revision);
        Assert.Equal(PolicyRevisionState.Rejected, rejected.State);
        Assert.Equal(3, rejected.Version);
        Assert.Equal("reviewer", rejected.Decision?.ActorId);
        Assert.Equal("Demand evidence is insufficient.", rejected.Decision?.Reason);
        Assert.Equal(PolicyRevisionState.Rejected, rejected.Decision?.Outcome);
        Assert.Equal(3, rejected.Decision?.RevisionVersion);
        Assert.Null(review.Decision);
    }

    [Fact]
    public void Reject_MissingReason_ReturnsNoRevision()
    {
        // Arrange
        var review = InReview();

        // Act
        var result = _engine.Reject(review, review.Version, "reviewer", " ");

        // Assert
        Assert.Equal(PolicyRevisionError.MissingRejectionReason, result.Error);
        Assert.Null(result.Revision);
        Assert.Equal(PolicyRevisionState.InReview, review.State);
    }

    [Fact]
    public void ReviseMaterial_ChangedContent_CreatesNewDraftInSamePolicyScope()
    {
        // Arrange
        var original = Draft();

        // Act
        var result = _engine.ReviseMaterial(original, original.Version, "revision-2", "editor", "content-2");

        // Assert
        var successor = Assert.IsType<PolicyRevision>(result.Revision);
        Assert.Equal("revision-2", successor.Id);
        Assert.Equal(original.Id, successor.PreviousRevisionId);
        Assert.Equal(original.PolicyId, successor.PolicyId);
        Assert.Equal(original.Scope, successor.Scope);
        Assert.Equal(PolicyRevisionState.Draft, successor.State);
        Assert.Equal(1, successor.Version);
        Assert.Equal("content-2", successor.ContentKey);
        Assert.Contains("planner", successor.MaterialContributors);
        Assert.Contains("editor", successor.MaterialContributors);
        Assert.Equal("content-1", original.ContentKey);
        Assert.Null(original.PreviousRevisionId);
    }

    [Fact]
    public void RetryRejected_RejectedRevision_CreatesNewDraftAndKeepsDecision()
    {
        // Arrange
        var rejected = Rejected();

        // Act
        var result = _engine.RetryRejected(rejected, rejected.Version, "revision-2", "planner-2");

        // Assert
        var retry = Assert.IsType<PolicyRevision>(result.Revision);
        Assert.Equal(PolicyRevisionState.Draft, retry.State);
        Assert.Equal(rejected.Id, retry.PreviousRevisionId);
        Assert.Equal(rejected.PolicyId, retry.PolicyId);
        Assert.Equal(rejected.Scope, retry.Scope);
        Assert.Equal(rejected.ContentKey, retry.ContentKey);
        Assert.Null(retry.Decision);
        Assert.Equal(PolicyRevisionState.Rejected, rejected.State);
        Assert.NotNull(rejected.Decision);
    }

    [Fact]
    public void RetryRejected_NonRejectedRevision_ReturnsConflict()
    {
        // Arrange
        var draft = Draft();

        // Act
        var result = _engine.RetryRejected(draft, draft.Version, "revision-2", "planner-2");

        // Assert
        Assert.Equal(PolicyRevisionError.InvalidTransition, result.Error);
        Assert.Null(result.Revision);
    }

    [Fact]
    public void ReviseMaterial_ApprovedRevision_PreservesApprovedDecision()
    {
        // Arrange
        var approved = Approved();

        // Act
        var result = _engine.ReviseMaterial(approved, approved.Version, "revision-2", "editor", "content-2");

        // Assert
        var successor = Assert.IsType<PolicyRevision>(result.Revision);
        Assert.Equal(PolicyRevisionState.Draft, successor.State);
        Assert.Equal(approved.Id, successor.PreviousRevisionId);
        Assert.Null(successor.Decision);
        Assert.Equal(PolicyRevisionState.Approved, approved.State);
        Assert.Equal("reviewer", approved.Decision?.ActorId);
    }

    [Fact]
    public void InvalidTransition_DraftApprovalAndTerminalSubmission_ReturnConflict()
    {
        // Arrange
        var draft = Draft();
        var approved = Approved();

        // Act
        var draftApproval = _engine.Approve(draft, draft.Version, "reviewer");
        var terminalSubmit = _engine.SubmitForReview(approved, approved.Version);

        // Assert
        Assert.Equal(PolicyRevisionError.InvalidTransition, draftApproval.Error);
        Assert.Null(draftApproval.Revision);
        Assert.Equal(PolicyRevisionError.InvalidTransition, terminalSubmit.Error);
        Assert.Null(terminalSubmit.Revision);
        Assert.Equal(PolicyRevisionState.Approved, approved.State);
    }

    [Fact]
    public void Approve_PreparerCannotApproveOwnRevision()
    {
        // Arrange
        var review = InReview();

        // Act
        var result = _engine.Approve(review, review.Version, "planner");

        // Assert
        Assert.Equal(PolicyRevisionError.SelfApproval, result.Error);
        Assert.Null(result.Revision);
        Assert.Equal(PolicyRevisionState.InReview, review.State);
    }

    [Fact]
    public void Approve_MaterialEditorCannotApproveSuccessor()
    {
        // Arrange
        var source = Draft();
        var changed = Assert.IsType<PolicyRevision>(
            _engine.ReviseMaterial(source, source.Version, "revision-2", "editor", "content-2").Revision);
        var review = Assert.IsType<PolicyRevision>(_engine.SubmitForReview(changed, changed.Version).Revision);

        // Act
        var result = _engine.Approve(review, review.Version, "editor");

        // Assert
        Assert.Equal(PolicyRevisionError.SelfApproval, result.Error);
        Assert.Null(result.Revision);
    }

    [Fact]
    public void Approve_InheritedPreparerCannotApproveSuccessor()
    {
        // Arrange
        var source = Draft();
        var changed = Assert.IsType<PolicyRevision>(
            _engine.ReviseMaterial(source, source.Version, "revision-2", "editor", "content-2").Revision);
        var review = Assert.IsType<PolicyRevision>(_engine.SubmitForReview(changed, changed.Version).Revision);

        // Act
        var result = _engine.Approve(review, review.Version, "planner");

        // Assert
        Assert.Equal(PolicyRevisionError.SelfApproval, result.Error);
        Assert.Null(result.Revision);
    }

    [Fact]
    public void Approve_IndependentSyntheticActor_ReturnsDecision()
    {
        // Arrange
        var review = InReview();

        // Act
        var result = _engine.Approve(review, review.Version, "reviewer");

        // Assert
        var approved = Assert.IsType<PolicyRevision>(result.Revision);
        Assert.Equal(PolicyRevisionState.Approved, approved.State);
        Assert.Equal("reviewer", approved.Decision?.ActorId);
        Assert.Null(approved.Decision?.Reason);
        Assert.Equal(3, approved.Decision?.RevisionVersion);
    }

    [Fact]
    public void StaleVersion_AllTransitionsAndSuccessors_ReturnConflict()
    {
        // Arrange
        var draft = Draft();
        var review = InReview();
        var rejected = Rejected();

        // Act
        var results = new[]
        {
            _engine.SubmitForReview(draft, 0),
            _engine.Approve(review, 1, "reviewer"),
            _engine.Reject(review, 1, "reviewer", "Reason"),
            _engine.ReviseMaterial(draft, 0, "revision-2", "editor", "content-2"),
            _engine.RetryRejected(rejected, 2, "revision-2", "planner-2")
        };

        // Assert
        Assert.All(results, result =>
        {
            Assert.Equal(PolicyRevisionError.VersionConflict, result.Error);
            Assert.Null(result.Revision);
        });
    }

    [Fact]
    public void TerminalRevision_ApproveRejectAndSubmit_DoNotMutate()
    {
        // Arrange
        var approved = Approved();
        var rejected = Rejected();

        // Act
        var approveAgain = _engine.Approve(approved, approved.Version, "reviewer-2");
        var rejectApproved = _engine.Reject(approved, approved.Version, "reviewer-2", "Reason");
        var rejectAgain = _engine.Reject(rejected, rejected.Version, "reviewer-2", "Reason");

        // Assert
        Assert.Equal(PolicyRevisionError.InvalidTransition, approveAgain.Error);
        Assert.Equal(PolicyRevisionError.InvalidTransition, rejectApproved.Error);
        Assert.Equal(PolicyRevisionError.InvalidTransition, rejectAgain.Error);
        Assert.Equal(PolicyRevisionState.Approved, approved.State);
        Assert.Equal(PolicyRevisionState.Rejected, rejected.State);
        Assert.Equal(3, approved.Version);
        Assert.Equal(3, rejected.Version);
    }

    [Fact]
    public void ReviseMaterial_LineageIdentityReuse_ReturnsConflict()
    {
        // Arrange
        var source = Draft();
        var child = Assert.IsType<PolicyRevision>(
            _engine.ReviseMaterial(source, source.Version, "revision-2", "editor", "content-2").Revision);

        // Act
        var result = _engine.ReviseMaterial(child, child.Version, "revision-1", "editor-2", "content-3");

        // Assert
        Assert.Equal(PolicyRevisionError.DuplicateRevisionId, result.Error);
        Assert.Null(result.Revision);
    }

    [Fact]
    public void ReviseMaterial_UnchangedContent_ReturnsNoSuccessor()
    {
        // Arrange
        var source = Draft();

        // Act
        var result = _engine.ReviseMaterial(source, source.Version, "revision-2", "editor", source.ContentKey);

        // Assert
        Assert.Equal(PolicyRevisionError.UnchangedContent, result.Error);
        Assert.Null(result.Revision);
    }

    private PolicyRevision Draft() =>
        Assert.IsType<PolicyRevision>(_engine.CreateDraft("revision-1", "policy-1", Scope, "planner", "content-1").Revision);

    private PolicyRevision InReview()
    {
        var draft = Draft();
        return Assert.IsType<PolicyRevision>(_engine.SubmitForReview(draft, draft.Version).Revision);
    }

    private PolicyRevision Approved()
    {
        var review = InReview();
        return Assert.IsType<PolicyRevision>(_engine.Approve(review, review.Version, "reviewer").Revision);
    }

    private PolicyRevision Rejected()
    {
        var review = InReview();
        return Assert.IsType<PolicyRevision>(
            _engine.Reject(review, review.Version, "reviewer", "Insufficient evidence.").Revision);
    }
}
