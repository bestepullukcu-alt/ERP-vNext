using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Handlers;

public sealed class EvaluatePerformanceReviewReadinessHandler : IRequestHandler<EvaluatePerformanceReviewReadinessCommand, Response<PerformanceReviewReadinessDto>>
{
    private readonly IPerformanceReviewReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluatePerformanceReviewReadinessHandler(IPerformanceReviewReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<PerformanceReviewReadinessDto>> Handle(EvaluatePerformanceReviewReadinessCommand request, CancellationToken ct)
    {
        var tenant = PerformanceReviewGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PerformanceReviewReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<PerformanceReviewReadinessDto>.Fail("PerformanceReview readiness record was not found.", 404);
        }

        PerformanceReviewGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<PerformanceReviewReadinessDto>.Success(PerformanceReviewMapper.ToDto(entity));
    }
}
