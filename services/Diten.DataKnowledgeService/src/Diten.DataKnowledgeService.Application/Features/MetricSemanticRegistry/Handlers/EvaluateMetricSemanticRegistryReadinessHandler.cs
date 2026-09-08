using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Commands;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Handlers;

public sealed class EvaluateMetricSemanticRegistryReadinessHandler : IRequestHandler<EvaluateMetricSemanticRegistryReadinessCommand, Response<MetricSemanticRegistryReadinessDto>>
{
    private readonly IMetricSemanticRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateMetricSemanticRegistryReadinessHandler(IMetricSemanticRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<MetricSemanticRegistryReadinessDto>> Handle(EvaluateMetricSemanticRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = MetricSemanticRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MetricSemanticRegistryReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<MetricSemanticRegistryReadinessDto>.Fail("MetricSemanticRegistry readiness record was not found.", 404);
        }

        MetricSemanticRegistryGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<MetricSemanticRegistryReadinessDto>.Success(MetricSemanticRegistryMapper.ToDto(entity));
    }
}
