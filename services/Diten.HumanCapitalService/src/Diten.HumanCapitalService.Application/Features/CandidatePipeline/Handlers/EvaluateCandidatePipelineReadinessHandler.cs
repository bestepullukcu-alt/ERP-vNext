using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Handlers;

public sealed class EvaluateCandidatePipelineReadinessHandler
    : IRequestHandler<EvaluateCandidatePipelineReadinessCommand, Response<CandidatePipelineReadinessDto>>
{
    private readonly ICandidatePipelineReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateCandidatePipelineReadinessHandler(
        ICandidatePipelineReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidatePipelineReadinessDto>> Handle(EvaluateCandidatePipelineReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidatePipelineGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidatePipelineReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<CandidatePipelineReadinessDto>.Fail("Candidate pipeline readiness record was not found.", 404);
        }

        CandidatePipelineGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<CandidatePipelineReadinessDto>.Success(CandidatePipelineMapper.ToDto(entity));
    }
}
