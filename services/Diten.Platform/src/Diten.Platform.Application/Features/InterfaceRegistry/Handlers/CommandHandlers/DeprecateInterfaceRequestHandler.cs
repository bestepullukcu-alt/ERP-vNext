using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.CommandHandlers;

public sealed class DeprecateInterfaceRequestHandler
    : IRequestHandler<DeprecateInterfaceRequest, Response<InterfaceActiveSnapshotDto>>
{
    private readonly IInterfaceRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IInterfaceRegistryAuditSink _auditSink;

    public DeprecateInterfaceRequestHandler(
        IInterfaceRegistryRepository repository,
        ICurrentUserContext currentUser,
        IInterfaceRegistryAuditSink auditSink)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditSink = auditSink;
    }

    public async Task<Response<InterfaceActiveSnapshotDto>> Handle(DeprecateInterfaceRequest request, CancellationToken ct)
    {
        var interfaceCode = InterfaceCodeNormalizer.Normalize(request.InterfaceCode);
        var version = request.Version.Trim().ToLowerInvariant();
        var activeSnapshot = await _repository.GetActiveSnapshotAsync(interfaceCode, version, ct);
        if (activeSnapshot is null)
        {
            return Response<InterfaceActiveSnapshotDto>.Fail("Active interface snapshot not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        var actor = InterfaceRegistryReviewSupport.ResolveActor(_currentUser);
        activeSnapshot.Definition.LifecycleStatus = InterfaceLifecycleStatus.Deprecated;
        activeSnapshot.DeprecationReason = request.Reason.Trim();
        activeSnapshot.DeprecatedAtUtc = now;
        activeSnapshot.DeprecatedBy = actor;
        activeSnapshot.SnapshotHash = InterfaceManifestHasher.HashSnapshot(activeSnapshot.Definition);
        await _repository.UpsertActiveSnapshotAsync(activeSnapshot, ct);

        var definition = InterfaceRegistryMapper.ToDefinition(activeSnapshot.Definition, activeSnapshot.ConfirmedAtUtc, activeSnapshot.ConfirmedBy ?? actor);
        definition.LifecycleStatus = InterfaceLifecycleStatus.Deprecated;
        definition.DeprecationReason = activeSnapshot.DeprecationReason;
        definition.DeprecatedAtUtc = now;
        definition.DeprecatedBy = actor;
        await _repository.UpsertDefinitionAsync(definition, ct);

        await _auditSink.EmitAsync("interface.deprecated", new Dictionary<string, string?>
        {
            ["interfaceCode"] = interfaceCode,
            ["interfaceVersion"] = version,
            ["actor"] = actor
        }, ct);

        return Response<InterfaceActiveSnapshotDto>.Success(InterfaceRegistryMapper.ToDto(activeSnapshot));
    }
}
