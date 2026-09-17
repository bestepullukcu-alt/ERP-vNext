using System.Reflection;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.PpmService.Tests.Portfolios;

public sealed class PortfolioOwnerAssignmentTests
{
    private static Portfolio New() => new(Guid.NewGuid(), Guid.NewGuid(), "P", "Portfolio", null, null);
    private static PortfolioOwnerAssignment Assign(Portfolio p, Guid? requestId = null) =>
        p.ChangeOwner(p.CreatedBy, Guid.NewGuid(), "Test human", "Initial responsibility",
            PortfolioOwnerOperation.Assign, null, p.Version, requestId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Draft_can_have_no_owner_and_assignment_does_not_come_from_creator()
    {
        var p = New();
        Assert.Null(p.CurrentOwnerAssignment);
        Assert.Empty(p.OwnerAssignments);
        Assert.Null(p.CapacityAllocationDescription);
    }

    [Fact]
    public void Transfer_appends_history_and_metadata_edit_preserves_it()
    {
        var p = New();
        var first = Assign(p);
        var next = p.ChangeOwner(p.CreatedBy, Guid.NewGuid(), "Other test human", "Handover",
            PortfolioOwnerOperation.Transfer, first.Id, 2, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        p.Update(p.CreatedBy, "P", "Updated", "Description", null, "Capacity explanation");
        Assert.Equal(4, p.Version);
        Assert.Equal(next, p.CurrentOwnerAssignment);
        Assert.Equal(new[] { first, next }, p.OwnerAssignments);
        Assert.Equal(first.Id, next.PreviousAssignmentId);
        Assert.Equal(DateTimeKind.Utc, next.OccurredAtUtc.Kind);
    }

    [Fact]
    public void Lost_response_retry_returns_original_receipt_without_second_effect()
    {
        var p = New();
        var first = Assign(p);
        var retry = p.ChangeOwner(first.ActorId, first.UserId, "Updated external label", first.Reason,
            first.Operation, null, first.ExpectedVersion, first.RequestId, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(first, retry);
        Assert.Equal(2, p.Version);
        Assert.Single(p.OwnerAssignments);
        Assert.Throws<InvalidOperationException>(() => p.ChangeOwner(first.ActorId, first.UserId,
            first.DisplayLabel, "Different reason", first.Operation, null, first.ExpectedVersion,
            first.RequestId, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Theory]
    [InlineData(PortfolioLifecycleState.Active)]
    [InlineData(PortfolioLifecycleState.Archived)]
    public void Existing_non_draft_metadata_and_owner_mutation_are_rejected(PortfolioLifecycleState state)
    {
        var p = New();
        p.Transition(p.CreatedBy, state);
        Assert.Throws<InvalidOperationException>(() => Assign(p));
        Assert.Throws<InvalidOperationException>(() => p.Update(p.CreatedBy, "P", "Changed", null, null));
        Assert.Equal(state, p.LifecycleState);
        Assert.Empty(p.OwnerAssignments);
    }

    [Fact]
    public void Invalid_reason_predecessor_target_or_version_never_mutates()
    {
        var p = New();
        Assert.Throws<ArgumentException>(() => p.ChangeOwner(p.CreatedBy, Guid.NewGuid(), "Human", " ",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        var first = Assign(p);
        Assert.Throws<InvalidOperationException>(() => p.ChangeOwner(p.CreatedBy, first.UserId, "Human", "Transfer",
            PortfolioOwnerOperation.Transfer, first.Id, 2, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => p.ChangeOwner(p.CreatedBy, Guid.NewGuid(), "Human", "Transfer",
            PortfolioOwnerOperation.Transfer, Guid.NewGuid(), 2, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => p.ChangeOwner(p.CreatedBy, Guid.NewGuid(), "Human", "Transfer",
            PortfolioOwnerOperation.Transfer, first.Id, 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Single(p.OwnerAssignments);
        Assert.Equal(2, p.Version);
    }

    [Fact]
    public void Real_Bson_roundtrip_keeps_embedded_receipt_and_old_shape_has_no_invented_owner()
    {
        typeof(PpmMongoContext).Assembly.GetType("Diten.PpmService.Persistence.Mongo.PpmBsonConfiguration")!
            .GetMethod("Configure", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null);
        var p = New();
        p.BindTemporaryNonProductionAccess(new PortfolioTemporaryNonProductionAccessBinding(
            PortfolioTemporaryNonProductionRecordAccessAuthority.DecisionKey,
            PortfolioTemporaryNonProductionRecordAccessAuthority.DecisionVersion,
            DateTime.UtcNow,
            p.Id));
        var first = Assign(p);
        var document = p.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<Portfolio>(document);
        Assert.Equal(first, restored.CurrentOwnerAssignment);
        Assert.Single(restored.OwnerAssignments);
        Assert.NotNull(restored.TemporaryNonProductionAccessBinding);
        Assert.Equal(p.TemporaryNonProductionAccessBinding!.DecisionKey, restored.TemporaryNonProductionAccessBinding.DecisionKey);
        Assert.Equal(p.TemporaryNonProductionAccessBinding.DecisionVersion, restored.TemporaryNonProductionAccessBinding.DecisionVersion);
        Assert.Equal(p.TemporaryNonProductionAccessBinding.PortfolioId, restored.TemporaryNonProductionAccessBinding.PortfolioId);
        Assert.Equal(
            p.TemporaryNonProductionAccessBinding.BoundAtUtc.Ticks -
                p.TemporaryNonProductionAccessBinding.BoundAtUtc.Ticks % TimeSpan.TicksPerMillisecond,
            restored.TemporaryNonProductionAccessBinding.BoundAtUtc.Ticks);
        document.Remove("OwnerAssignments");
        document.Remove("CapacityAllocationDescription");
        document.Remove("TemporaryNonProductionAccessBinding");
        var old = BsonSerializer.Deserialize<Portfolio>(document);
        Assert.Null(old.CurrentOwnerAssignment);
        Assert.Null(old.CapacityAllocationDescription);
        Assert.Equal(p.CreatedBy, old.CreatedBy);
        Assert.Null(old.TemporaryNonProductionAccessBinding);
    }

    [Fact]
    public void Temporary_binding_is_create_only_and_rejects_malformed_or_rebound_values()
    {
        var p = New();
        var binding = new PortfolioTemporaryNonProductionAccessBinding(
            PortfolioTemporaryNonProductionRecordAccessAuthority.DecisionKey,
            PortfolioTemporaryNonProductionRecordAccessAuthority.DecisionVersion,
            DateTime.UtcNow,
            p.Id);
        p.BindTemporaryNonProductionAccess(binding);
        Assert.Equal(binding, p.TemporaryNonProductionAccessBinding);
        Assert.Throws<InvalidOperationException>(() => p.BindTemporaryNonProductionAccess(binding));
        var malformed = binding with { DecisionVersion = "", PortfolioId = Guid.NewGuid() };
        Assert.False(malformed.HasValidShape());
        Assert.Throws<InvalidOperationException>(() => New().BindTemporaryNonProductionAccess(malformed));
    }

    [Fact]
    public async Task Transfer_recomputes_creator_and_current_owner_relationships_without_history_access()
    {
        var p = New();
        var authority = new PortfolioTemporaryNonProductionRecordAccessAuthority(
            enabled: true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction);
        p.BindTemporaryNonProductionAccess(authority.CreateBinding(p.Id));
        var initialOwner = Guid.NewGuid();
        var nextOwner = Guid.NewGuid();
        var first = p.ChangeOwner(p.CreatedBy, initialOwner, "Initial owner", "Assignment",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        p.ChangeOwner(p.CreatedBy, nextOwner, "Next owner", "Transfer", PortfolioOwnerOperation.Transfer,
            first.Id, 2, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        async Task<PortfolioAuthorityOutcome> Access(Guid actorId, string operation) =>
            (await authority.EvaluateAsync(new(p.TenantId, actorId, p.Id, operation, p.Version,
                RecordTenantId: p.TenantId, CreatorId: p.CreatedBy,
                CurrentOwnerUserId: p.CurrentOwnerAssignment!.UserId, LifecycleState: p.LifecycleState,
                TemporaryNonProductionAccessBinding: p.TemporaryNonProductionAccessBinding), default)).Outcome;

        Assert.Equal(PortfolioAuthorityOutcome.Allowed, await Access(p.CreatedBy, "read"));
        Assert.Equal(PortfolioAuthorityOutcome.Denied, await Access(initialOwner, "read"));
        Assert.Equal(PortfolioAuthorityOutcome.Allowed, await Access(nextOwner, "read"));
        Assert.Equal(PortfolioAuthorityOutcome.Denied, await Access(p.CreatedBy, "history-read"));
    }

    [Fact]
    public void Evidence_rejects_wrong_scope_and_expired_or_unversioned_decisions()
    {
        var scope = new PortfolioAuthorityScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "assign-owner");
        var evidence = new PortfolioAuthorityEvidence(scope, PortfolioAuthorityOutcome.Allowed,
            "test-policy", DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.True(evidence.IsBoundTo(scope));
        Assert.False(evidence.IsBoundTo(scope with { ActorId = Guid.NewGuid() }));
        Assert.False(evidence.IsBoundTo(scope with { TargetUserId = Guid.NewGuid() }));
        Assert.False((evidence with { ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1) }).IsBoundTo(scope));
        Assert.False((evidence with { PolicyVersion = "" }).IsBoundTo(scope));
    }
}
