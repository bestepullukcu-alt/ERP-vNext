using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;
using Diten.ManagementGovernanceService.Application.Features.Dws.Validators;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.Dws;

public sealed class DwsFunctionalProviderFixtureTests
{
    public static IEnumerable<object[]> Fu16Failures()
    {
        yield return [DwsErrors.AuthenticationRequired, 401];
        yield return [DwsErrors.PermissionDenied, 403];
        yield return [DwsErrors.AuthorizationAuthorityUnavailable, 503];
    }

    public static IEnumerable<object[]> Mod0117Failures()
    {
        yield return [DwsErrors.InvalidContextReference, 400];
        yield return [DwsErrors.ResourceNotFound, 404];
        yield return [DwsErrors.ExternalContextConflict, 409];
        yield return [DwsErrors.ExternalContextAuthorityUnavailable, 503];
    }

    [Theory]
    [MemberData(nameof(Fu16Failures))]
    public async Task FU16_negative_dispositions_are_exact_and_never_reach_context_or_persistence(string code, int status)
    {
        var commands = new ProbeCommandPort();
        var contexts = new ProbeContext();
        var handler = new CreateStructureHandler(new CreateStructureValidator(), new RejectAuthorization(code), contexts, commands);

        var response = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal([code], response.Errors);
        Assert.Equal(0, contexts.Calls);
        Assert.Equal(0, commands.Calls);
    }

    [Theory]
    [MemberData(nameof(Mod0117Failures))]
    public async Task MOD0117_negative_dispositions_are_exact_and_never_reach_persistence(string code, int status)
    {
        var commands = new ProbeCommandPort();
        var handler = new CreateStructureHandler(new CreateStructureValidator(), new AllowAuthorization(), new RejectContext(code), commands);

        var response = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal([code], response.Errors);
        Assert.Equal(0, commands.Calls);
    }

    private static CreateStructureCommand CreateCommand() => new(
        new(new("ppm.external-context-reference", "1.0", ExternalContextKind.Project, Guid.NewGuid()), "Plan", null),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "key"));

    private sealed class AllowAuthorization : IFu16DwsFunctionalAuthorization
    {
        public Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(DwsTrustedActorContext context, string moduleCode, string moduleEntitlementCode, string operation, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(new DwsFu16AuthorizationSnapshot(
                context.TenantId, context.SecuritySubjectId, context.EffectiveActorId, context.DelegatedActorId,
                moduleCode, moduleEntitlementCode, operation, permission, true, 1, 1, 1, 1));
        public Task RevalidateAsync(DwsTrustedActorContext context, DwsFu16AuthorizationSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RejectAuthorization(string code) : IFu16DwsFunctionalAuthorization
    {
        public Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(DwsTrustedActorContext context, string moduleCode, string moduleEntitlementCode, string operation, string permission, CancellationToken cancellationToken) => throw new DwsValidationException(code);
        public Task RevalidateAsync(DwsTrustedActorContext context, DwsFu16AuthorizationSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ProbeContext : IMod0117DwsContextValidator
    {
        public int Calls { get; private set; }
        public Task<DwsMod0117ContextSnapshot> ValidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new DwsMod0117ContextSnapshot(context.TenantId, context.EffectiveActorId, context.DelegatedActorId, reference, 1));
        }
        public Task RevalidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, DwsMod0117ContextSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RejectContext(string code) : IMod0117DwsContextValidator
    {
        public Task<DwsMod0117ContextSnapshot> ValidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, CancellationToken cancellationToken)
        {
            if (code == DwsErrors.ResourceNotFound) throw new DwsNotFoundException();
            if (code == DwsErrors.ExternalContextConflict) throw new DwsConflictException(code);
            throw new DwsValidationException(code);
        }
        public Task RevalidateAsync(DwsTrustedActorContext context, ExternalContextReference reference, DwsMod0117ContextSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ProbeCommandPort : IDwsFunctionalCommandPort
    {
        public int Calls { get; private set; }
        public Task<CreateStructureResult> CreateStructureAsync(CreateStructureRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken) { Calls++; return Task.FromResult(new CreateStructureResult(Guid.NewGuid(), 1, 1, 1)); }
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
