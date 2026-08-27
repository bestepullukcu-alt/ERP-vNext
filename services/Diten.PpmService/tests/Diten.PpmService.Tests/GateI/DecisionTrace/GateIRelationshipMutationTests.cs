using System.Security.Cryptography;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;
using Diten.Shared.Core;
using Xunit;

namespace Diten.PpmService.Tests.GateI.DecisionTrace;

public sealed class GateIRelationshipMutationTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _aggregate = Guid.NewGuid();

    [Fact]
    public async Task Attach_uses_exact_authority_and_persistence_scope()
    {
        var authority = new Authority(Accepted());
        var persistence = new Persistence();
        var command = Attach();

        var result = await Handler(authority, persistence).Handle(command, default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, authority.Calls);
        Assert.Equal(1, persistence.InvestmentCalls);
        Assert.Equal(command.OperationId, authority.Last!.OperationId);
        Assert.Equal(_tenant, authority.Last.TenantId);
        Assert.Equal(_actor, authority.Last.ActorId);
        Assert.Equal(command.IdempotencyKey, persistence.LastScope!.IdempotencyKey);
        Assert.Equal(Accepted().ProvenanceHash, persistence.LastScope.ProvenanceHash);
    }

    [Fact]
    public async Task Matching_receipt_replays_without_authority_or_write()
    {
        var stored = new GateIRelationshipMutationResult(_aggregate, 7, "stored", false);
        var authority = new Authority(Accepted());
        var persistence = new Persistence
        {
            Receipt = new GateIReceiptResult(GateIReceiptDisposition.Matching, stored)
        };

        var result = await Handler(authority, persistence).Handle(Attach(), default);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.Replayed);
        Assert.Equal(0, authority.Calls);
        Assert.Equal(0, persistence.InvestmentCalls);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(503)]
    public async Task Authority_terminal_results_do_not_write(int statusCode)
    {
        var authority = new Authority(new(statusCode, "authority_terminal", Hex("authority"), false));
        var persistence = new Persistence();

        var result = await Handler(authority, persistence).Handle(Attach(), default);

        Assert.Equal(statusCode, result.StatusCode);
        Assert.Equal(0, persistence.InvestmentCalls);
    }

    [Fact]
    public async Task Malformed_authority_provenance_is_503_without_write()
    {
        var persistence = new Persistence();
        var result = await Handler(new Authority(new(200, "accepted", "NOT-A-HASH", true)), persistence)
            .Handle(Attach(), default);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(0, persistence.InvestmentCalls);
    }

    [Fact]
    public async Task Malformed_wrapper_is_400_without_persistence_execution()
    {
        var persistence = new Persistence { InvokeMutation = true };
        var malformed = Attach() with { CanonicalWrapperUtf8 = "{}"u8.ToArray() };

        var result = await Handler(new Authority(Accepted()), persistence).Handle(malformed, default);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, persistence.InvestmentCalls);
    }

    [Fact]
    public async Task Remove_is_ppm_owned_and_does_not_call_remote_authority()
    {
        var authority = new Authority(new(503, "must_not_call", "", false));
        var persistence = new Persistence();
        var command = new GateIRelationshipMutationCommand(
            _aggregate, GateIRelationshipKind.GoverningDecision, GateIRelationshipAction.Remove,
            [], null, 1, "remove-key", "ppm.investment-cases.gate-i.governing-decision.remove");

        var result = await Handler(authority, persistence).Handle(command, default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, authority.Calls);
        Assert.Equal(1, persistence.InvestmentCalls);
    }

    [Fact]
    public async Task Access_denial_precedes_receipt_and_authority()
    {
        var authority = new Authority(Accepted());
        var persistence = new Persistence();
        var result = await Handler(authority, persistence, PpmAccessDecision.Forbidden).Handle(Attach(), default);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(0, persistence.ReconcileCalls);
        Assert.Equal(0, authority.Calls);
    }

    [Fact]
    public async Task Idempotency_payload_conflict_is_409_before_authority()
    {
        var authority = new Authority(Accepted());
        var persistence = new Persistence
        {
            Receipt = new GateIReceiptResult(GateIReceiptDisposition.Conflict)
        };

        var result = await Handler(authority, persistence).Handle(Attach(), default);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, authority.Calls);
        Assert.Equal(0, persistence.InvestmentCalls);
    }

    [Theory]
    [InlineData(0, "ppm.investment-cases.gate-i.governing-decision.attach")]
    [InlineData(1, "PPM.investment-cases.gate-i.governing-decision.attach")]
    [InlineData(1, "ppm.investment-cases.gate-i.governing-decision.attach ")]
    public async Task Expected_version_and_operation_identity_are_fail_closed(
        int expectedVersion,
        string operationId)
    {
        var authority = new Authority(Accepted());
        var persistence = new Persistence();
        var result = await Handler(authority, persistence).Handle(
            Attach() with { ExpectedVersion = expectedVersion, OperationId = operationId }, default);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, persistence.ReconcileCalls);
        Assert.Equal(0, authority.Calls);
    }

    [Fact]
    public async Task Request_hash_binds_tenant_and_actor()
    {
        var command = Attach();
        var first = new Persistence();
        var second = new Persistence();
        await Handler(new Authority(Accepted()), first).Handle(command, default);
        var otherHandler = new GateIRelationshipMutationHandler(
            new Authority(Accepted()), second, new Access(PpmAccessDecision.Allowed),
            new Tenant(Guid.NewGuid()), new Actor(Guid.NewGuid()), new Correlation());
        await otherHandler.Handle(command, default);

        Assert.NotEqual(first.LastScope!.RequestHash, second.LastScope!.RequestHash);
    }

    private GateIRelationshipMutationHandler Handler(
        Authority authority,
        Persistence persistence,
        PpmAccessDecision access = PpmAccessDecision.Allowed) =>
        new(authority, persistence, new Access(access), new Tenant(_tenant), new Actor(_actor), new Correlation());

    private GateIRelationshipMutationCommand Attach()
    {
        var reference = new GoverningDecisionReferenceV1(
            new InvestmentCaseContextV1(_aggregate),
            new DecisionRevisionReferenceV1(Guid.NewGuid(), Guid.NewGuid(), 1));
        return new GateIRelationshipMutationCommand(
            _aggregate,
            GateIRelationshipKind.GoverningDecision,
            GateIRelationshipAction.AttachOrReplace,
            DecisionTraceReferenceCodec.Serialize(reference),
            null,
            1,
            "attach-key",
            "ppm.investment-cases.gate-i.governing-decision.attach");
    }

    private static GateIAuthorityValidationResult Accepted() =>
        new(200, "accepted", Hex("accepted-provenance"), true);

    private static string Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class Authority(GateIAuthorityValidationResult result) : IGateIRelationshipAuthority
    {
        public int Calls { get; private set; }
        public GateIAuthorityValidationRequest? Last { get; private set; }
        public Task<GateIAuthorityValidationResult> ValidateAsync(GateIAuthorityValidationRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            Last = request;
            return Task.FromResult(result);
        }
    }

    private sealed class Persistence : IGateIRelationshipMutationPersistence
    {
        public GateIReceiptResult Receipt { get; init; } = new(GateIReceiptDisposition.Missing);
        public bool InvokeMutation { get; init; }
        public int ReconcileCalls { get; private set; }
        public int InvestmentCalls { get; private set; }
        public GateIMutationScope? LastScope { get; private set; }

        public Task<GateIReceiptResult> ReconcileAsync(GateIMutationScope scope, CancellationToken cancellationToken)
        {
            ReconcileCalls++;
            LastScope = scope;
            return Task.FromResult(Receipt);
        }

        public Task<GateIRelationshipMutationResult> ExecuteInvestmentCaseAsync(
            GateIMutationScope scope, Guid aggregateId, int expectedVersion, Action<InvestmentCase> mutation,
            string mutationName, CancellationToken cancellationToken)
        {
            InvestmentCalls++;
            LastScope = scope;
            if (InvokeMutation)
                mutation(new InvestmentCase(
                    scope.TenantId, Guid.NewGuid(), "TEST", "Test", null, Guid.NewGuid(), null, null));
            return Task.FromResult(new GateIRelationshipMutationResult(aggregateId, expectedVersion + 1, "mutated", false));
        }

        public Task<GateIRelationshipMutationResult> ExecuteBenefitCommitmentAsync(
            GateIMutationScope scope, Guid aggregateId, int expectedVersion, Action<BenefitCommitment> mutation,
            string mutationName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed record Tenant(Guid TenantId) : ITenantContext;
    private sealed record Actor(Guid ActorId) : ICurrentActorContext;
    private sealed class Correlation : ICorrelationContext { public Guid CorrelationId { get; } = Guid.NewGuid(); }
    private sealed class Access(PpmAccessDecision decision) : IPpmAccessAuthorizer
    {
        public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken cancellationToken) =>
            Task.FromResult(decision);
    }
}
