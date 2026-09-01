using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;
using Diten.ManagementGovernanceService.Application.Features.Dws.Validators;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalSecurityConsistencyMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public void Query_store_exposes_snapshot_or_version_revalidation_seam_for_mixed_snapshot_prevention()
    {
        var seamTypes = typeof(DwsFunctionalQueryStore).GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Concat(typeof(DwsFunctionalQueryStore).GetMethods()
                .Where(method => method.DeclaringType == typeof(DwsFunctionalQueryStore))
                .SelectMany(method => method.GetParameters()))
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.Contains(seamTypes, typeName =>
            typeName.Contains("Session", StringComparison.Ordinal)
            || typeName.Contains("Snapshot", StringComparison.Ordinal)
            || typeName.Contains("Fence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Receipt_schema_canonicalization_outcome_domain_and_result_corruption_fail_closed()
    {
        var corruptions = new (string Field, BsonValue Value)[]
        {
            ("RequestCanonicalizationVersion", "wrong"),
            ("OutcomeSchemaVersion", "wrong"),
            ("OutcomeKind", "unknown"),
            ("DomainCode", "wrong"),
            ("StableOutcomeJson", "{\"domainCode\":\"wrong\",\"outcomeKind\":\"succeeded\",\"result\":{}}")
        };

        foreach (var (field, value) in corruptions)
        {
            await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
            var tenant = Guid.NewGuid();
            var actor = scope.CommandActor(tenant, "corrupt");
            var request = new CreateStructureRequest(DwsFunctionalMongoScope.Reference(), "Corrupt", null);
            await scope.Commands.CreateStructureAsync(request, actor, default);
            var filter = Builders<BsonDocument>.Filter.Eq("TenantId", Standard(tenant))
                & Builders<BsonDocument>.Filter.Eq("IdempotencyKey", "corrupt");
            await scope.Context.Collection("receipts").UpdateOneAsync(filter,
                Builders<BsonDocument>.Update
                    .Set("SecuritySubjectId", Standard(actor.SecuritySubjectId))
                    .Set(field, value));

            await Assert.ThrowsAsync<DwsValidationException>(() =>
                scope.Commands.CreateStructureAsync(request, actor, default));
        }
    }

    [Fact]
    public async Task Receipt_replay_revalidates_current_visibility_before_returning_success()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "visibility");
        var request = new CreateStructureRequest(DwsFunctionalMongoScope.Reference(), "Visibility", null);
        var result = await scope.Commands.CreateStructureAsync(request, actor, default);
        await scope.Context.Collection("receipts").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("TenantId", Standard(tenant)),
            Builders<BsonDocument>.Update.Set("SecuritySubjectId", Standard(actor.SecuritySubjectId)));
        await scope.Context.Collection("definitions").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", Standard(result.StructureDefinitionId)),
            Builders<BsonDocument>.Update.Set("IsDeleted", true).Set("DeletedAtUtc", DateTime.UtcNow));

        await Assert.ThrowsAsync<DwsNotFoundException>(() => scope.Commands.CreateStructureAsync(request, actor, default));
    }

    [Fact]
    public async Task Concurrent_identical_create_reconciles_one_receipt_and_same_result()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "parallel", Guid.NewGuid());
        var request = new CreateStructureRequest(DwsFunctionalMongoScope.Reference(), "Parallel", null);
        using var gate = new Barrier(2);

        async Task<CreateStructureResult> Execute()
        {
            gate.SignalAndWait();
            return await scope.Commands.CreateStructureAsync(request, actor, default);
        }

        var results = await Task.WhenAll(Task.Run(Execute), Task.Run(Execute));
        Assert.Equal(results[0], results[1]);
        var counts = await scope.CountsAsync(tenant);
        Assert.Equal(1, counts["definitions"]);
        Assert.Equal(1, counts["revisions"]);
        Assert.Equal(1, counts["receipts"]);
        Assert.Equal(1, counts["audit-intents"]);
        Assert.Equal(1, counts["outbox"]);
    }

    [Fact]
    public async Task FU16_freshness_change_between_decision_and_prewrite_is_503_with_zero_command_calls()
    {
        var authorization = new ChangingAuthorization();
        var contexts = new StableContext();
        var commands = new CountingCommands();
        var request = Request();
        var response = await new CreateStructureHandler(new CreateStructureValidator(), authorization, contexts, commands)
            .Handle(request, default);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(2, authorization.Calls);
        Assert.Equal(0, commands.Calls);
    }

    [Fact]
    public async Task MOD0117_fence_change_between_decision_and_prewrite_is_409_with_zero_command_calls()
    {
        var authorization = new StableAuthorization();
        var contexts = new ChangingContext();
        var commands = new CountingCommands();
        var response = await new CreateStructureHandler(new CreateStructureValidator(), authorization, contexts, commands)
            .Handle(Request(), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(2, contexts.Calls);
        Assert.Equal(0, commands.Calls);
    }

    private static CreateStructureCommand Request() => new(
        new(DwsFunctionalMongoScope.Reference(), "Fence", null),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "fence"));

    private static BsonBinaryData Standard(Guid value) => new(value, GuidRepresentation.Standard);

    private sealed class ChangingAuthorization : IFu16DwsFunctionalAuthorization
    {
        public int Calls { get; private set; }
        public Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(
            DwsTrustedActorContext context, string moduleCode, string moduleEntitlementCode,
            string operation, string permission, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new DwsFu16AuthorizationSnapshot(
                context.TenantId, context.SecuritySubjectId, context.EffectiveActorId, context.DelegatedActorId,
                moduleCode, moduleEntitlementCode, operation, permission, true, 1, 1, 1, 1));
        }
        public Task RevalidateAsync(DwsTrustedActorContext context, DwsFu16AuthorizationSnapshot snapshot, CancellationToken cancellationToken)
        {
            Calls++;
            throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
        }
    }

    private sealed class StableAuthorization : IFu16DwsFunctionalAuthorization
    {
        public Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(
            DwsTrustedActorContext context, string moduleCode, string moduleEntitlementCode,
            string operation, string permission, CancellationToken cancellationToken) => Task.FromResult(
                new DwsFu16AuthorizationSnapshot(context.TenantId, context.SecuritySubjectId, context.EffectiveActorId,
                    context.DelegatedActorId, moduleCode, moduleEntitlementCode, operation, permission, true, 1, 1, 1, 1));
        public Task RevalidateAsync(DwsTrustedActorContext context, DwsFu16AuthorizationSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StableContext : IMod0117DwsContextValidator
    {
        public Task<DwsMod0117ContextSnapshot> ValidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, CancellationToken cancellationToken) =>
            Task.FromResult(new DwsMod0117ContextSnapshot(context.TenantId, context.EffectiveActorId, context.DelegatedActorId, reference, 1));
        public Task RevalidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, DwsMod0117ContextSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ChangingContext : IMod0117DwsContextValidator
    {
        public int Calls { get; private set; }
        public Task<DwsMod0117ContextSnapshot> ValidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new DwsMod0117ContextSnapshot(context.TenantId, context.EffectiveActorId, context.DelegatedActorId, reference, 1));
        }
        public Task RevalidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, DwsMod0117ContextSnapshot snapshot, CancellationToken cancellationToken)
        {
            Calls++;
            throw new DwsConflictException(DwsErrors.ExternalContextConflict);
        }
    }

    private sealed class CountingCommands : IDwsFunctionalCommandPort
    {
        public int Calls { get; private set; }
        public Task<CreateStructureResult> CreateStructureAsync(CreateStructureRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new CreateStructureResult(Guid.NewGuid(), 1, 1, 1)); }
        public Task<UpdateStructureMetadataResult> UpdateStructureMetadataAsync(UpdateStructureMetadataRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AddStructureNodeResult> AddStructureNodeAsync(AddStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoveStructureNodeResult> MoveStructureNodeAsync(MoveStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReorderStructureNodeResult> ReorderStructureNodeAsync(ReorderStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RemoveStructureNodeResult> RemoveStructureNodeAsync(RemoveStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AddStructuralDependencyResult> AddStructuralDependencyAsync(AddStructuralDependencyRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RemoveStructuralDependencyResult> RemoveStructuralDependencyAsync(RemoveStructuralDependencyRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CreateStructureBaselineResult> CreateStructureBaselineAsync(CreateStructureBaselineRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CreateNextStructureRevisionResult> CreateNextStructureRevisionAsync(CreateNextStructureRevisionRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
