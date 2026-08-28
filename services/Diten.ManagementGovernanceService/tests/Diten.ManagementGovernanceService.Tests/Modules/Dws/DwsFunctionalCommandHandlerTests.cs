using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;
using Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.QueryHandlers;
using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using Diten.ManagementGovernanceService.Application.Features.Dws.Validators;
using DwsOutcomeKind = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsOutcomeKind;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.Dws;

public sealed class DwsFunctionalCommandHandlerTests
{
    [Fact]
    public async Task Create_handler_returns_201_and_calls_authorization_context_and_typed_port_once()
    {
        var authorization = new AllowAuthorization();
        var contexts = new AllowContext();
        var commands = new CommandPort();
        var command = new CreateStructureCommand(
            new(new("ppm.external-context-reference", "1.0", ExternalContextKind.Project, Guid.NewGuid()), "Plan", null),
            CommandContext());

        var response = await new CreateStructureHandler(new CreateStructureValidator(), authorization, contexts, commands)
            .Handle(command, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(1, authorization.Calls);
        Assert.Equal(1, contexts.Calls);
        Assert.Equal(1, commands.CreateCalls);
    }

    [Fact]
    public async Task Same_state_metadata_result_remains_typed_200_no_op()
    {
        var commands = new CommandPort { MetadataOutcome = DwsOutcomeKind.NoOp };
        var command = new UpdateStructureMetadataCommand(new(Guid.NewGuid(), "Plan", null, 1), CommandContext());

        var response = await new UpdateStructureMetadataHandler(
            new UpdateStructureMetadataValidator(), new AllowAuthorization(), new AllowContext(), new AllowVisibility(), commands)
            .Handle(command, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("no-op", response.Data!.OutcomeKind);
        Assert.Equal(1, commands.MetadataCalls);
    }

    [Fact]
    public async Task Query_handler_returns_typed_200_projection()
    {
        var definitionId = Guid.NewGuid();
        var queries = new QueryPort(definitionId);
        var query = new GetStructureByIdQuery(definitionId, QueryContext());

        var response = await new GetStructureByIdHandler(
            new GetStructureByIdValidator(), new AllowAuthorization(), new AllowContext(), new AllowVisibility(), queries)
            .Handle(query, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(definitionId, response.Data!.StructureDefinitionId);
        Assert.Equal(1, queries.Calls);
    }

    private static DwsTrustedActorContext CommandContext() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "key");
    private static DwsTrustedActorContext QueryContext() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null);

    private sealed class AllowAuthorization : IFu16DwsFunctionalAuthorization
    {
        public int Calls { get; private set; }
        public Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(DwsTrustedActorContext context, string moduleCode, string moduleEntitlementCode, string operation, string permission, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new DwsFu16AuthorizationSnapshot(
                context.TenantId, context.SecuritySubjectId, context.EffectiveActorId, context.DelegatedActorId,
                moduleCode, moduleEntitlementCode, operation, permission, true, 1, 1, 1, 1));
        }

        public Task RevalidateAsync(DwsTrustedActorContext context, DwsFu16AuthorizationSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AllowContext : IMod0117DwsContextValidator
    {
        public int Calls { get; private set; }
        public Task<DwsMod0117ContextSnapshot> ValidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new DwsMod0117ContextSnapshot(context.TenantId, context.EffectiveActorId, context.DelegatedActorId, reference, 1));
        }

        public Task RevalidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, DwsMod0117ContextSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AllowVisibility : IDwsStructureVisibilityPort
    {
        private static readonly ExternalContextReference Reference = new(
            ExternalContextReference.RequiredContractName,
            ExternalContextReference.RequiredContractVersion,
            ExternalContextKind.Project,
            Guid.Parse("10000000-0000-0000-0000-000000000001"));

        public Task<DwsStructureVisibilitySnapshot> CaptureAsync(
            Guid structureDefinitionId, DwsTrustedActorContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new DwsStructureVisibilitySnapshot(context.TenantId, structureDefinitionId, 1, Reference));

        public Task RevalidateAsync(
            DwsTrustedActorContext context, DwsStructureVisibilitySnapshot snapshot, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class CommandPort : IDwsFunctionalCommandPort
    {
        public int CreateCalls { get; private set; }
        public int MetadataCalls { get; private set; }
        public DwsOutcomeKind MetadataOutcome { get; init; } = DwsOutcomeKind.Succeeded;
        public Task<CreateStructureResult> CreateStructureAsync(CreateStructureRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) { CreateCalls++; return Task.FromResult(new CreateStructureResult(Guid.NewGuid(), 1, 1, 1)); }
        public Task<UpdateStructureMetadataResult> UpdateStructureMetadataAsync(UpdateStructureMetadataRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) { MetadataCalls++; return Task.FromResult(new UpdateStructureMetadataResult(request.StructureDefinitionId, 1, 1, MetadataOutcome)); }
        public Task<AddStructureNodeResult> AddStructureNodeAsync(AddStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoveStructureNodeResult> MoveStructureNodeAsync(MoveStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReorderStructureNodeResult> ReorderStructureNodeAsync(ReorderStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RemoveStructureNodeResult> RemoveStructureNodeAsync(RemoveStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AddStructuralDependencyResult> AddStructuralDependencyAsync(AddStructuralDependencyRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RemoveStructuralDependencyResult> RemoveStructuralDependencyAsync(RemoveStructuralDependencyRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CreateStructureBaselineResult> CreateStructureBaselineAsync(CreateStructureBaselineRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CreateNextStructureRevisionResult> CreateNextStructureRevisionAsync(CreateNextStructureRevisionRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class QueryPort(Guid definitionId) : IDwsFunctionalQueryPort
    {
        public int Calls { get; private set; }
        public Task<StructureSummaryDto> GetStructureByIdAsync(Guid structureDefinitionId, DwsTrustedActorContext context, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new StructureSummaryDto(definitionId, new("ppm.external-context-reference", "1.0", ExternalContextKind.Project, Guid.NewGuid()), 1, 1, 1));
        }
        public Task<StructureTreeDto> GetStructureTreeAsync(Guid structureDefinitionId, int? revisionNumber, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StructureValidationDto> ValidateStructureAsync(Guid structureDefinitionId, int? revisionNumber, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StructureComparisonDto> CompareStructureRevisionsAsync(Guid structureDefinitionId, int leftRevisionNumber, int rightRevisionNumber, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BaselineComparisonDto> CompareStructureBaselinesAsync(Guid structureDefinitionId, int leftBaselineNumber, int rightBaselineNumber, DwsTrustedActorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
