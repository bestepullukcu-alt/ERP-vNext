using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalVisibilityMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task All_nine_post_create_commands_and_five_queries_are_non_disclosing_with_zero_writes()
    {
        await AssertScenarioAsync(VisibilityScenario.ActorInvisible);
        await AssertScenarioAsync(VisibilityScenario.CrossTenant);
        await AssertScenarioAsync(VisibilityScenario.SoftDeleted);
    }

    [Fact]
    public async Task Accepted_query_validates_and_revalidates_the_stored_context_reference_for_the_effective_actor()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "visibility-create");
        var reference = DwsFunctionalMongoScope.Reference();
        var created = await scope.Commands.CreateStructureAsync(new(reference, "Visible", null), actor, default);
        var probe = new ContextProbe(actor.EffectiveActorId, reference, rejectActor: false);
        await using var provider = Services(scope, probe).BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GetStructureByIdQuery(
            created.StructureDefinitionId,
            DwsFunctionalMongoScope.QueryActor(actor)));

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(reference, response.Data!.ExternalContextReference);
        Assert.Equal(1, probe.ValidateCalls);
        Assert.Equal(1, probe.RevalidateCalls);
        Assert.Equal(reference, probe.LastReference);
        Assert.Equal(actor.EffectiveActorId, probe.LastEffectiveActorId);
    }

    private async Task AssertScenarioAsync(VisibilityScenario scenario)
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "visibility-create");
        var reference = DwsFunctionalMongoScope.Reference();
        var created = await scope.Commands.CreateStructureAsync(new(reference, "Visibility", null), actor, default);
        var requestActor = DwsFunctionalMongoScope.QueryActor(actor);
        var probe = new ContextProbe(actor.EffectiveActorId, reference, scenario == VisibilityScenario.ActorInvisible);

        if (scenario == VisibilityScenario.CrossTenant)
            requestActor = requestActor with { TenantId = Guid.NewGuid() };
        if (scenario == VisibilityScenario.ActorInvisible)
            requestActor = requestActor with { EffectiveActorId = Guid.NewGuid() };
        if (scenario == VisibilityScenario.SoftDeleted)
        {
            await scope.Context.Collection("definitions").UpdateOneAsync(
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq(
                    "_id", new MongoDB.Bson.BsonBinaryData(created.StructureDefinitionId, MongoDB.Bson.GuidRepresentation.Standard)),
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Combine(
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("IsDeleted", true),
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("DeletedAtUtc", DateTime.UtcNow)));
        }

        var before = await scope.CountsAsync(tenant);
        var requestTenantBefore = await scope.CountsAsync(requestActor.TenantId);
        if (scenario is VisibilityScenario.CrossTenant or VisibilityScenario.SoftDeleted)
        {
            var visibilityError = await Assert.ThrowsAsync<DwsNotFoundException>(() =>
                new DwsStructureVisibilityPort(scope.QueryStore).CaptureAsync(
                    created.StructureDefinitionId, requestActor, default));
            Assert.Equal(DwsErrors.ResourceNotFound, visibilityError.Code);
        }
        await using var provider = Services(scope, probe).BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        foreach (var request in Requests(created.StructureDefinitionId, requestActor))
        {
            var response = await sender.Send(request);
            Assert.NotNull(response);
            Assert.Equal(404, (int)response.GetType().GetProperty("StatusCode")!.GetValue(response)!);
            Assert.False((bool)response.GetType().GetProperty("IsSuccessful")!.GetValue(response)!);
            Assert.Equal(before, await scope.CountsAsync(tenant));
            Assert.Equal(requestTenantBefore, await scope.CountsAsync(requestActor.TenantId));
        }

        if (scenario == VisibilityScenario.ActorInvisible)
        {
            Assert.Equal(14, probe.ValidateCalls);
            Assert.Equal(0, probe.RevalidateCalls);
            Assert.Equal(reference, probe.LastReference);
            Assert.Equal(requestActor.EffectiveActorId, probe.LastEffectiveActorId);
        }
        else
        {
            Assert.Equal(0, probe.ValidateCalls);
            Assert.Equal(0, probe.RevalidateCalls);
        }
    }

    private static ServiceCollection Services(DwsFunctionalMongoScope scope, ContextProbe contexts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDwsApplication();
        services.AddSingleton<IDwsFunctionalCommandPort>(scope.Commands);
        services.AddSingleton<IDwsFunctionalQueryPort>(scope.Queries);
        services.AddSingleton<IDwsStructureVisibilityPort>(new DwsStructureVisibilityPort(scope.QueryStore));
        services.AddSingleton<IFu16DwsFunctionalAuthorization, AllowAuthorization>();
        services.AddSingleton<IMod0117DwsContextValidator>(contexts);
        return services;
    }

    private static IReadOnlyList<object> Requests(Guid definitionId, DwsTrustedActorContext queryActor)
    {
        var commandActor = queryActor with { IdempotencyKey = "visibility-command" };
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        return
        [
            new UpdateStructureMetadataCommand(new(definitionId, "Changed", null, 1), commandActor),
            new AddStructureNodeCommand(new(definitionId, null, "NODE", "Node", null, 0, 1), commandActor),
            new MoveStructureNodeCommand(new(definitionId, first, null, 0, 1), commandActor),
            new ReorderStructureNodeCommand(new(definitionId, first, 0, 1), commandActor),
            new RemoveStructureNodeCommand(new(definitionId, first, 1), commandActor),
            new AddStructuralDependencyCommand(new(definitionId, first, second, 1), commandActor),
            new RemoveStructuralDependencyCommand(new(definitionId, first, second, 1), commandActor),
            new CreateStructureBaselineCommand(new(definitionId, 1), commandActor),
            new CreateNextStructureRevisionCommand(new(definitionId, 1, null, 1), commandActor),
            new GetStructureByIdQuery(definitionId, queryActor),
            new GetStructureTreeQuery(definitionId, null, queryActor),
            new ValidateStructureQuery(definitionId, null, queryActor),
            new CompareStructureRevisionsQuery(definitionId, 1, 2, queryActor),
            new CompareStructureBaselinesQuery(definitionId, 1, 2, queryActor)
        ];
    }

    private enum VisibilityScenario { ActorInvisible, CrossTenant, SoftDeleted }

    private sealed class AllowAuthorization : IFu16DwsFunctionalAuthorization
    {
        public Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(
            DwsTrustedActorContext context, string moduleCode, string moduleEntitlementCode,
            string operation, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(new DwsFu16AuthorizationSnapshot(
                context.TenantId, context.SecuritySubjectId, context.EffectiveActorId, context.DelegatedActorId,
                moduleCode, moduleEntitlementCode, operation, permission, true, 1, 1, 1, 1));

        public Task RevalidateAsync(
            DwsTrustedActorContext context, DwsFu16AuthorizationSnapshot snapshot, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ContextProbe(Guid expectedActor, ExternalContextReference expectedReference, bool rejectActor)
        : IMod0117DwsContextValidator
    {
        public int ValidateCalls { get; private set; }
        public int RevalidateCalls { get; private set; }
        public Guid LastEffectiveActorId { get; private set; }
        public ExternalContextReference? LastReference { get; private set; }

        public Task<DwsMod0117ContextSnapshot> ValidateAsync(
            DwsTrustedActorContext context, ExternalContextReference reference, CancellationToken cancellationToken)
        {
            ValidateCalls++;
            LastEffectiveActorId = context.EffectiveActorId;
            LastReference = reference;
            if (rejectActor || context.EffectiveActorId != expectedActor)
                throw new DwsNotFoundException();
            Assert.Equal(expectedReference, reference);
            return Task.FromResult(new DwsMod0117ContextSnapshot(
                context.TenantId, context.EffectiveActorId, context.DelegatedActorId, reference, 1));
        }

        public Task RevalidateAsync(
            DwsTrustedActorContext context, ExternalContextReference reference,
            DwsMod0117ContextSnapshot snapshot, CancellationToken cancellationToken)
        {
            RevalidateCalls++;
            LastEffectiveActorId = context.EffectiveActorId;
            LastReference = reference;
            Assert.Equal(expectedActor, context.EffectiveActorId);
            Assert.Equal(expectedReference, reference);
            Assert.Equal(reference, snapshot.Reference);
            return Task.CompletedTask;
        }
    }
}
