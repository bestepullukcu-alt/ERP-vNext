using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Handlers;

public sealed class EvaluateLearningTrainingReadinessHandler : IRequestHandler<EvaluateLearningTrainingReadinessCommand, Response<LearningTrainingReadinessDto>>
{
    private readonly ILearningTrainingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateLearningTrainingReadinessHandler(ILearningTrainingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<LearningTrainingReadinessDto>> Handle(EvaluateLearningTrainingReadinessCommand request, CancellationToken ct)
    {
        var tenant = LearningTrainingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<LearningTrainingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<LearningTrainingReadinessDto>.Fail("LearningTraining readiness record was not found.", 404);
        }

        LearningTrainingGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<LearningTrainingReadinessDto>.Success(LearningTrainingMapper.ToDto(entity));
    }
}
