using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.Mongo;
using Diten.PpmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.Portfolios;

[Collection(PpmMongoCollection.CollectionName)]
public sealed class PortfolioOwnerAssignmentMongoTests(PpmDisposableMongo mongo)
{
    // Existing explicitly selected disposable profile only; never appsettings or a shared DB.
    private async Task<Fixture> Create()
    {
        var database = PpmMongoTestDatabase.Open(mongo.ReplicaSetConnectionString);
        await new PpmMongoIndexInitializer(database).StartAsync(default);
        return new(new PpmMongoContext(database.Client, database));
    }

    [Fact]
    public async Task Actual_service_creates_edits_assigns_transfers_and_replays_in_one_aggregate()
    {
        var f = await Create();
        var created = await f.Service().Create(new("P", "Portfolio", "Description", null, "Capacity"), default);
        Assert.Equal(201, created.StatusCode);
        var id = created.Data!.Id;
        var assignment = new ChangePortfolioOwnerCommand(id, Guid.NewGuid(), "Responsibility",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid());
        var receipt = await new ChangePortfolioOwnerHandler(f.Service()).Handle(assignment, default);
        Assert.Equal(200, receipt.StatusCode);
        var retry = await f.Service().ChangeOwner(assignment, default);
        Assert.Equal(receipt.Data, retry.Data);
        Assert.Equal(409, (await f.Service().ChangeOwner(assignment with { Reason = "different" }, default)).StatusCode);
        var transfer = assignment with { TargetUserId = Guid.NewGuid(), Reason = "Handover",
            Operation = PortfolioOwnerOperation.Transfer, ExpectedAssignmentId = receipt.Data!.AssignmentId,
            ExpectedVersion = 2, RequestId = Guid.NewGuid() };
        Assert.Equal(200, (await f.Service().ChangeOwner(transfer, default)).StatusCode);
        Assert.Equal(200, (await f.Service().Update(new(id, "P", "Renamed", null, null, 3, "Revised capacity"), default)).StatusCode);
        var saved = await f.Repository.GetByIdAsync(f.TenantId, id, default);
        Assert.Equal(4, saved!.Version);
        Assert.Equal(2, saved.OwnerAssignments.Count);
        Assert.Equal("Revised capacity", saved.CapacityAllocationDescription);
        Assert.Equal(4, await f.AuditCount(id));
        var intent = await f.Context.AuditIntents.Find(x => x.Id == saved.OwnerAssignments[0].AuditIntentId).SingleAsync();
        Assert.Equal(saved.OwnerAssignments[0].CorrelationId, intent.CorrelationId);
        Assert.Equal("updated", intent.Mutation);
    }

    [Fact]
    public async Task Permission_target_and_missing_authority_are_independent_and_close_their_surfaces()
    {
        var f = await Create();
        var p = await f.Seed();
        var command = new ChangePortfolioOwnerCommand(p.Id, Guid.NewGuid(), "Assignment", PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid());
        f.External.Permission = false;
        Assert.Equal(403, (await f.Service().ChangeOwner(command, default)).StatusCode);
        f.External.Permission = true;
        f.External.OwnerOutcome = PortfolioAuthorityOutcome.Denied;
        Assert.Equal(403, (await f.Service().ChangeOwner(command, default)).StatusCode);
        f.External.OwnerOutcome = PortfolioAuthorityOutcome.Allowed;
        f.External.WrongBinding = true;
        Assert.Equal(503, (await f.Service().ChangeOwner(command, default)).StatusCode);
        f.External.WrongBinding = false;
        f.External.Human = false;
        Assert.Equal(403, (await f.Service().ChangeOwner(command, default)).StatusCode);
        f.External.Human = true;
        f.External.OwnerOutcome = PortfolioAuthorityOutcome.Unavailable;
        Assert.Equal(503, (await f.Service().ChangeOwner(command, default)).StatusCode);
        Assert.Equal(503, (await f.Service(false).Create(new("N", "New", null, null), default)).StatusCode);
        Assert.Equal(503, (await f.Service(false).List(new(), default)).StatusCode);
        Assert.Equal(503, (await f.Service(false).GetById(new(p.Id), default)).StatusCode);
        Assert.Equal(503, (await f.Service(false).Update(new(p.Id, "P", "Edit", null, null, 1), default)).StatusCode);
        Assert.Equal(503, (await f.Service(false).ChangeOwner(command, default)).StatusCode);
        Assert.Equal(503, (await f.Service(false).OwnerCandidates(new(p.Id), default)).StatusCode);
        var saved = await f.Repository.GetByIdAsync(f.TenantId, p.Id, default);
        Assert.Equal(1, saved!.Version);
        Assert.Empty(saved.OwnerAssignments);
        Assert.Equal(0, await f.AuditCount(p.Id));
    }

    [Fact]
    public async Task Positive_external_evidence_does_not_replace_the_actor_record_relation()
    {
        var f = await Create();
        var unrelated = f.ServiceFor(Guid.NewGuid());
        var unowned = await f.Seed("UNRELATED-ASSIGN");
        var assign = new ChangePortfolioOwnerCommand(unowned.Id, Guid.NewGuid(), "Assignment",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid());

        Assert.Equal(403, (await unrelated.OwnerCandidates(new(unowned.Id), default)).StatusCode);
        Assert.Equal(403, (await unrelated.ChangeOwner(assign, default)).StatusCode);
        var unownedAfterDenied = await f.Repository.GetByIdAsync(f.TenantId, unowned.Id, default);
        Assert.Equal(1, unownedAfterDenied!.Version);
        Assert.Empty(unownedAfterDenied.OwnerAssignments);
        Assert.Equal(0, await f.AuditCount(unowned.Id));

        var owned = await f.Seed("UNRELATED-TRANSFER");
        var initial = await f.Service().ChangeOwner(new(owned.Id, Guid.NewGuid(), "Initial",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid()), default);
        Assert.Equal(200, initial.StatusCode);
        var transfer = new ChangePortfolioOwnerCommand(owned.Id, Guid.NewGuid(), "Transfer",
            PortfolioOwnerOperation.Transfer, initial.Data!.AssignmentId, 2, Guid.NewGuid());
        Assert.Equal(403, (await unrelated.ChangeOwner(transfer, default)).StatusCode);
        var ownedAfterDenied = await f.Repository.GetByIdAsync(f.TenantId, owned.Id, default);
        Assert.Equal(2, ownedAfterDenied!.Version);
        Assert.Single(ownedAfterDenied.OwnerAssignments);
        Assert.Equal(1, await f.AuditCount(owned.Id));

        // Creator need not remain current owner: the separate PMO and target evidence is still required.
        Assert.Equal(200, (await f.Service().ChangeOwner(transfer, default)).StatusCode);
    }

    [Fact]
    public async Task List_and_details_do_not_disclose_hidden_records_or_unauthorized_history()
    {
        var f = await Create();
        var p = await f.Seed();
        var other = await f.SeedForActor(Guid.NewGuid(), "HIDDEN");
        var assigned = await f.Service().ChangeOwner(new(p.Id, Guid.NewGuid(), "Confidential reason",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid()), default);
        Assert.Equal(200, assigned.StatusCode);
        var list = await f.Service().List(new(), default);
        Assert.Single(list.Data!);
        Assert.Null(list.Data![0].OwnerHistory);
        Assert.Equal(404, (await f.Service().GetById(new(other.Id), default)).StatusCode);
        var detail = await f.Service().GetById(new(p.Id), default);
        Assert.Null(detail.Data!.OwnerHistory);
        var differentTenant = new Fixture(f.Context);
        Assert.Equal(404, (await differentTenant.Service().GetById(new(p.Id), default)).StatusCode);
        Assert.Equal(404, (await differentTenant.Service().ChangeOwner(new(p.Id, Guid.NewGuid(), "Cross tenant",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid()), default)).StatusCode);
    }

    [Theory]
    [InlineData(PortfolioLifecycleState.Draft)]
    [InlineData(PortfolioLifecycleState.Active)]
    [InlineData(PortfolioLifecycleState.Archived)]
    public async Task Lifecycle_delete_are_closed_and_existing_non_draft_records_stay_read_only(PortfolioLifecycleState state)
    {
        var f = await Create();
        var p = await f.Seed(state: state);
        Assert.Equal(409, (await f.Service().Transition(new(p.Id, PortfolioLifecycleState.Active, p.Version), default)).StatusCode);
        Assert.Equal(409, (await f.Service().SoftDelete(new(p.Id, p.Version), default)).StatusCode);
        if (state != PortfolioLifecycleState.Draft)
        {
            // Non-Draft and legacy records never enter the temporary Draft access path.
            Assert.Equal(404, (await f.Service().Update(new(p.Id, "P", "Edit", null, null, p.Version), default)).StatusCode);
            Assert.Equal(404, (await f.Service().ChangeOwner(new(p.Id, Guid.NewGuid(), "Owner",
                PortfolioOwnerOperation.Assign, null, p.Version, Guid.NewGuid()), default)).StatusCode);
            Assert.Equal(404, (await f.Service().GetById(new(p.Id), default)).StatusCode);
        }
        Assert.Equal(state, (await f.Repository.GetByIdAsync(f.TenantId, p.Id, default))!.LifecycleState);
        Assert.Equal(0, await f.AuditCount(p.Id));
    }

    [Fact]
    public async Task Authoritative_create_preserves_normalized_uniqueness_and_stale_edit_is_409()
    {
        var f = await Create();
        var created = await f.Service().Create(new("Cafe\u0301", "Portfolio", null, null), default);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(409, (await f.Service().Create(new("Café", "Duplicate", null, null), default)).StatusCode);
        Assert.Equal(409, (await f.Service().Update(new(created.Data!.Id, "Café", "Changed", null, null, 99), default)).StatusCode);
        Assert.Equal(1, (await f.Repository.GetByIdAsync(f.TenantId, created.Data.Id, default))!.Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_service_requests_have_one_effect_and_same_request_can_recover(bool sameRequest)
    {
        var f = await Create();
        var p = await f.Seed();
        var first = new ChangePortfolioOwnerCommand(p.Id, Guid.NewGuid(), "Responsibility",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid());
        var second = sameRequest ? first : first with { RequestId = Guid.NewGuid(), TargetUserId = Guid.NewGuid() };
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        f.External.BeforeOwnerReply = async () =>
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(true);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        };
        var results = await Task.WhenAll(f.Service().ChangeOwner(first, default), f.Service().ChangeOwner(second, default));
        Assert.Equal(new[] { 200, 409 }, results.Select(x => x.StatusCode).Order().ToArray());
        f.External.BeforeOwnerReply = null;
        var stored = await f.Repository.GetByIdAsync(f.TenantId, p.Id, default);
        Assert.Single(stored!.OwnerAssignments);
        Assert.Equal(2, stored.Version);
        Assert.Equal(1, await f.AuditCount(p.Id));
        if (sameRequest)
            Assert.Equal(results.Single(x => x.StatusCode == 200).Data, (await f.Service().ChangeOwner(first, default)).Data);
    }

    [Fact]
    public async Task Update_permission_is_not_assignment_permission_and_target_denial_is_not_unavailable()
    {
        var f = await Create();
        var p = await f.Seed();
        var command = new ChangePortfolioOwnerCommand(p.Id, Guid.NewGuid(), "Owner", PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid());
        f.External.DeniedPermission = PpmPermissions.PortfoliosAssignOwner;
        Assert.Equal(403, (await f.Service().ChangeOwner(command, default)).StatusCode);
        Assert.Equal(200, (await f.Service().Update(new(p.Id, "P", "Metadata editor", null, null, 1), default)).StatusCode);
        f.External.DeniedPermission = null;
        command = command with { ExpectedVersion = 2 };
        f.External.TargetOutcome = PortfolioAuthorityOutcome.Denied;
        Assert.Equal(403, (await f.Service().ChangeOwner(command, default)).StatusCode);
        f.External.TargetOutcome = PortfolioAuthorityOutcome.Unavailable;
        Assert.Equal(503, (await f.Service().ChangeOwner(command, default)).StatusCode);
        f.External.TargetOutcome = PortfolioAuthorityOutcome.Allowed;
        f.External.Active = false;
        Assert.Equal(403, (await f.Service().ChangeOwner(command, default)).StatusCode);
        f.External.Active = true;
        f.External.SameTenant = false;
        Assert.Equal(403, (await f.Service().ChangeOwner(command, default)).StatusCode);
        Assert.Empty((await f.Repository.GetByIdAsync(f.TenantId, p.Id, default))!.OwnerAssignments);
    }

    [Theory]
    [InlineData(PortfolioOwnerLabelState.Missing, null, "PORTFOLIO_OWNER_LABEL_MISSING")]
    [InlineData(PortfolioOwnerLabelState.TooLong, "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX", "PORTFOLIO_OWNER_LABEL_TOO_LONG")]
    public async Task Valid_label_policy_rejections_have_no_portfolio_or_audit_effect(
        PortfolioOwnerLabelState labelState, string? displayLabel, string expectedError)
    {
        var f = await Create();
        var portfolio = await f.Seed();
        f.External.LabelState = labelState;
        f.External.DisplayLabel = displayLabel;

        var result = await f.Service().ChangeOwner(new(portfolio.Id, Guid.NewGuid(), "Assignment",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid()), default);

        Assert.Equal(409, result.StatusCode);
        Assert.Contains(expectedError, result.Errors);
        var stored = await f.Repository.GetByIdAsync(f.TenantId, portfolio.Id, default);
        Assert.Equal(1, stored!.Version);
        Assert.Empty(stored.OwnerAssignments);
        Assert.Equal(0, await f.AuditCount(portfolio.Id));
    }

    [Fact]
    public async Task Transaction_reassertion_preserves_definitive_status_and_has_zero_owner_effect()
    {
        var cases = new[]
        {
            new ReassertionCase(PortfolioAuthorityOutcome.NotFound, PortfolioOwnerLabelState.Available, false, 404, null),
            new ReassertionCase(PortfolioAuthorityOutcome.Denied, PortfolioOwnerLabelState.Available, false, 403, null),
            new ReassertionCase(PortfolioAuthorityOutcome.Unavailable, PortfolioOwnerLabelState.Available, false, 503, null),
            new ReassertionCase(PortfolioAuthorityOutcome.Allowed, PortfolioOwnerLabelState.Missing, false, 409, "PORTFOLIO_OWNER_LABEL_MISSING"),
            new ReassertionCase(PortfolioAuthorityOutcome.Allowed, PortfolioOwnerLabelState.TooLong, false, 409, "PORTFOLIO_OWNER_LABEL_TOO_LONG"),
            new ReassertionCase(PortfolioAuthorityOutcome.Allowed, PortfolioOwnerLabelState.Available, true, 503, null)
        };

        foreach (var testCase in cases)
        {
            var f = await Create();
            var portfolio = await f.Seed($"REASSERT-{Guid.NewGuid():N}");
            var evaluations = 0;
            f.External.BeforeOwnerReply = () =>
            {
                if (++evaluations == 2)
                {
                    f.External.TargetOutcome = testCase.TargetOutcome;
                    f.External.LabelState = testCase.LabelState;
                    f.External.DisplayLabel = testCase.LabelState == PortfolioOwnerLabelState.TooLong
                        ? new string('X', 201) : "Test human";
                    f.External.WrongBinding = testCase.WrongBinding;
                }
                return Task.CompletedTask;
            };

            var result = await f.Service().ChangeOwner(new(portfolio.Id, Guid.NewGuid(), "Reassertion",
                PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid()), default);

            Assert.Equal(testCase.ExpectedStatus, result.StatusCode);
            Assert.Null(result.Data); // no owner receipt is created by a failed transaction reassertion
            if (testCase.ExpectedError is not null) Assert.Contains(testCase.ExpectedError, result.Errors);
            var stored = await f.Repository.GetByIdAsync(f.TenantId, portfolio.Id, default);
            Assert.Equal(1, stored!.Version);
            Assert.Empty(stored.OwnerAssignments);
            Assert.Equal(0, await f.AuditCount(portfolio.Id));
            Assert.Equal(2, evaluations);
        }
    }

    [Fact]
    public async Task Real_CAS_writers_cannot_commit_two_owners()
    {
        var f = await Create();
        var p = await f.Seed();
        var first = await f.Repository.GetByIdAsync(f.TenantId, p.Id, default);
        var stale = await f.Repository.GetByIdAsync(f.TenantId, p.Id, default);
        first!.ChangeOwner(f.ActorId, Guid.NewGuid(), "Human", "First", PortfolioOwnerOperation.Assign,
            null, 1, Guid.NewGuid(), Guid.NewGuid(), f.CorrelationId);
        stale!.ChangeOwner(f.ActorId, Guid.NewGuid(), "Human", "Second", PortfolioOwnerOperation.Assign,
            null, 1, Guid.NewGuid(), Guid.NewGuid(), f.CorrelationId);
        await f.Unit.ExecuteInTransactionAsync(async ct => { await f.Repository.ReplaceAsync(first, 1, ct); return true; }, default);
        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => f.Unit.ExecuteInTransactionAsync(async ct =>
        { await f.Repository.ReplaceAsync(stale, 1, ct); return true; }, default));
        var saved = await f.Repository.GetByIdAsync(f.TenantId, p.Id, default);
        Assert.Single(saved!.OwnerAssignments);
        Assert.Equal(first.CurrentOwnerAssignment, saved.CurrentOwnerAssignment);
    }

    [Fact]
    public async Task Real_transaction_abort_rolls_back_owner_history_receipt_version_and_audit()
    {
        var f = await Create();
        var p = await f.Seed();
        var assignment = p.ChangeOwner(f.ActorId, Guid.NewGuid(), "Human", "Aborted",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid(), Guid.NewGuid(), f.CorrelationId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Unit.ExecuteInTransactionAsync<bool>(async ct =>
        {
            await f.Repository.ReplaceAsync(p, 1, ct);
            await f.Audit.AddAsync(new(assignment.AuditIntentId, f.TenantId, f.ActorId, f.CorrelationId,
                nameof(Portfolio), p.Id, "updated", assignment.OccurredAtUtc), ct);
            throw new InvalidOperationException("Test-owned transaction abort after real writes.");
        }, default));
        var saved = await f.Repository.GetByIdAsync(f.TenantId, p.Id, default);
        Assert.Equal(1, saved!.Version);
        Assert.Empty(saved.OwnerAssignments);
        Assert.Equal(0, await f.AuditCount(p.Id));
    }

    private sealed class Fixture(PpmMongoContext context) : ITenantContext, ICurrentActorContext, ICorrelationContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public PpmMongoContext Context => context;
        public PortfolioRepository Repository { get; } = new(context);
        public AuditIntentRepository Audit { get; } = new(context);
        public PpmUnitOfWork Unit { get; } = new(context);
        public ExternalAuthority External { get; } = new();
        public PortfolioService Service(bool authority = true) => new(Repository, Audit, Unit, this, this, this,
            External,
            recordAuthority: authority
                ? new PortfolioTemporaryNonProductionRecordAccessAuthority(
                    enabled: true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction)
                : null,
            ownerAuthority: authority ? External : null);
        public PortfolioService ServiceFor(Guid actorId, bool authority = true) => new(Repository, Audit, Unit, this,
            new TestActorContext(actorId), this, External,
            recordAuthority: authority
                ? new PortfolioTemporaryNonProductionRecordAccessAuthority(
                    enabled: true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction)
                : null,
            ownerAuthority: authority ? External : null);
        public Task<long> AuditCount(Guid id) => Context.AuditIntents.CountDocumentsAsync(x => x.TenantId == TenantId && x.EntityId == id);
        public async Task<Portfolio> Seed(string code = "P", PortfolioLifecycleState state = PortfolioLifecycleState.Draft)
            => await SeedForActor(ActorId, code, state);

        public async Task<Portfolio> SeedForActor(Guid creatorId, string code = "P",
            PortfolioLifecycleState state = PortfolioLifecycleState.Draft)
        {
            var p = new Portfolio(TenantId, creatorId, code, "Portfolio", null, null);
            p.BindTemporaryNonProductionAccess(
                new PortfolioTemporaryNonProductionRecordAccessAuthority(
                    enabled: true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction)
                    .CreateBinding(p.Id));
            if (state != PortfolioLifecycleState.Draft) p.Transition(creatorId, state);
            await Unit.ExecuteInTransactionAsync(async ct => { await Repository.AddAsync(p, ct); return true; }, default);
            return p;
        }

        private sealed record TestActorContext(Guid ActorId) : ICurrentActorContext;
    }

    private sealed record ReassertionCase(PortfolioAuthorityOutcome TargetOutcome,
        PortfolioOwnerLabelState LabelState, bool WrongBinding, int ExpectedStatus, string? ExpectedError);

    // Test assembly only: deterministic external identity/access answers; no aggregate/repository/UoW doubles.
    private sealed class ExternalAuthority : IPpmAccessAuthorizer, IPortfolioOwnerActionAuthority
    {
        public bool Permission = true, Human = true, Active = true, SameTenant = true, WrongBinding;
        public PortfolioOwnerLabelState LabelState = PortfolioOwnerLabelState.Available;
        public string? DisplayLabel = "Test human";
        public string? DeniedPermission;
        public Func<Task>? BeforeOwnerReply;
        public PortfolioAuthorityOutcome TargetOutcome = PortfolioAuthorityOutcome.Allowed;
        public PortfolioAuthorityOutcome OwnerOutcome = PortfolioAuthorityOutcome.Allowed;
        public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken ct) =>
            Task.FromResult(Permission && permission != DeniedPermission ? PpmAccessDecision.Allowed : PpmAccessDecision.Forbidden);
        private PortfolioAuthorityEvidence Evidence(PortfolioAuthorityScope s, PortfolioAuthorityOutcome outcome) =>
            new(WrongBinding ? s with { ActorId = Guid.NewGuid() } : s, outcome, "TEST-ONLY-policy",
                DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddMinutes(2), "TEST-ONLY-policy-binding");
        public Task<PortfolioAuthorityEvidence> CanManageAsync(PortfolioAuthorityScope s, CancellationToken ct) =>
            Task.FromResult(Evidence(s, OwnerOutcome));
        public async Task<PortfolioOwnerEvidence> EvaluateAsync(PortfolioAuthorityScope s, CancellationToken ct)
        {
            if (BeforeOwnerReply is not null) await BeforeOwnerReply();
            return new PortfolioOwnerEvidence(Evidence(s, OwnerOutcome), Evidence(s, TargetOutcome),
                SameTenant, Active, Human, true, DisplayLabel, LabelState);
        }
        public Task<PortfolioOwnerCandidatesEvidence> CandidatesAsync(PortfolioAuthorityScope s, CancellationToken ct) =>
            Task.FromResult(new PortfolioOwnerCandidatesEvidence(Evidence(s, OwnerOutcome), []));
    }
}
