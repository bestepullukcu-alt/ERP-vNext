using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Handlers;

public sealed class EvaluateMentorshipRecommendationNetworkReadinessHandler : IRequestHandler<EvaluateMentorshipRecommendationNetworkReadinessCommand, Response<MentorshipRecommendationNetworkReadinessDto>>
{
    private readonly IMentorshipRecommendationNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateMentorshipRecommendationNetworkReadinessHandler(IMentorshipRecommendationNetworkReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<MentorshipRecommendationNetworkReadinessDto>> Handle(EvaluateMentorshipRecommendationNetworkReadinessCommand request, CancellationToken ct)
    {
        var tenant = MentorshipRecommendationNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MentorshipRecommendationNetworkReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<MentorshipRecommendationNetworkReadinessDto>.Fail("MentorshipRecommendationNetwork readiness record was not found.", 404);
        }

        MentorshipRecommendationNetworkGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<MentorshipRecommendationNetworkReadinessDto>.Success(MentorshipRecommendationNetworkMapper.ToDto(entity));
    }
}
