using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Commands;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Handlers;

public sealed class DeleteMetricSemanticRegistryReadinessHandler : IRequestHandler<DeleteMetricSemanticRegistryReadinessCommand, Response<bool>>
{
    private readonly IMetricSemanticRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteMetricSemanticRegistryReadinessHandler(IMetricSemanticRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteMetricSemanticRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = MetricSemanticRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("MetricSemanticRegistry readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.MetricSemanticRegistryReadinessState = MetricSemanticRegistryReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
