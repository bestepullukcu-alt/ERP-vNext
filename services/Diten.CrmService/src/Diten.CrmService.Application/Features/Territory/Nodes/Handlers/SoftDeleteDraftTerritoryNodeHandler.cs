using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Territory.Models.Handlers;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes.Handlers;

public sealed class SoftDeleteDraftTerritoryNodeHandler
    : IRequestHandler<SoftDeleteDraftTerritoryNodeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryLifecycleAuditPublisher _audit;

    public SoftDeleteDraftTerritoryNodeHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryLifecycleAuditPublisher audit)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(SoftDeleteDraftTerritoryNodeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
            return Response<bool>.Fail("Tenant context is required.", 400);
        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
            return Response<bool>.Fail("Territory model not found.", 404);
        var node = await _nodes.GetByIdAsync(tenantId, request.ModelId, request.Id, cancellationToken);
        if (node is null)
            return Response<bool>.Fail("Territory node not found.", 404);

        if (!TerritoryLifecycle.Is(model.Status, TerritoryLifecycle.Draft)
            || !TerritoryLifecycle.Is(node.Status, TerritoryLifecycle.Draft))
        {
            await _audit.PublishAsync(TerritoryLifecycleAuditEvents.NodeDeleteRejected,
                new TerritoryLifecycleAuditPayload(tenantId, model.Id, node.Id, node.Status, node.Status, null,
                    "authenticated-user", request.Reason?.Trim(), request.CorrelationId?.Trim(), DateTimeOffset.UtcNow),
                cancellationToken);
            return Response<bool>.Fail("Only a draft node in a draft model can be soft-deleted.", 409);
        }

        node.IsDeleted = true;
        node.DeletedAt = DateTimeOffset.UtcNow;
        node.UpdatedAt = node.DeletedAt;
        node.CorrelationId = request.CorrelationId?.Trim();
        await _nodes.UpdateAsync(node, cancellationToken);
        await _audit.PublishAsync(TerritoryLifecycleAuditEvents.NodeSoftDeleted,
            new TerritoryLifecycleAuditPayload(tenantId, model.Id, node.Id, TerritoryLifecycle.Draft, "soft-deleted",
                null, "authenticated-user", request.Reason?.Trim(), request.CorrelationId?.Trim(), DateTimeOffset.UtcNow),
            cancellationToken);
        return Response<bool>.Success(true);
    }
}
