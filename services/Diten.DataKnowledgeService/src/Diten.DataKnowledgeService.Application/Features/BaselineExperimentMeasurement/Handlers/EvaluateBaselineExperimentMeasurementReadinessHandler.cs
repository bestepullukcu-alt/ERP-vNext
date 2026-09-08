using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Commands;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Handlers;

public sealed class EvaluateBaselineExperimentMeasurementReadinessHandler : IRequestHandler<EvaluateBaselineExperimentMeasurementReadinessCommand, Response<BaselineExperimentMeasurementReadinessDto>>
{
    private readonly IBaselineExperimentMeasurementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateBaselineExperimentMeasurementReadinessHandler(IBaselineExperimentMeasurementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<BaselineExperimentMeasurementReadinessDto>> Handle(EvaluateBaselineExperimentMeasurementReadinessCommand request, CancellationToken ct)
    {
        var tenant = BaselineExperimentMeasurementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<BaselineExperimentMeasurementReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<BaselineExperimentMeasurementReadinessDto>.Fail("BaselineExperimentMeasurement readiness record was not found.", 404);
        }

        BaselineExperimentMeasurementGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<BaselineExperimentMeasurementReadinessDto>.Success(BaselineExperimentMeasurementMapper.ToDto(entity));
    }
}
