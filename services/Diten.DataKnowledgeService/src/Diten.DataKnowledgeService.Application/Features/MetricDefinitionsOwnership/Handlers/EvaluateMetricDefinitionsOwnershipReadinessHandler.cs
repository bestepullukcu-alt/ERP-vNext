using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Commands;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Handlers;

public sealed class EvaluateMetricDefinitionsOwnershipReadinessHandler : IRequestHandler<EvaluateMetricDefinitionsOwnershipReadinessCommand, Response<MetricDefinitionsOwnershipReadinessDto>>
{
    private readonly IMetricDefinitionsOwnershipReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateMetricDefinitionsOwnershipReadinessHandler(IMetricDefinitionsOwnershipReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<MetricDefinitionsOwnershipReadinessDto>> Handle(EvaluateMetricDefinitionsOwnershipReadinessCommand request, CancellationToken ct)
    {
        var tenant = MetricDefinitionsOwnershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MetricDefinitionsOwnershipReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<MetricDefinitionsOwnershipReadinessDto>.Fail("MetricDefinitionsOwnership readiness record was not found.", 404);
        }

        MetricDefinitionsOwnershipGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<MetricDefinitionsOwnershipReadinessDto>.Success(MetricDefinitionsOwnershipMapper.ToDto(entity));
    }
}
