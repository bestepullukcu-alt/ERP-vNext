using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Handlers;

public sealed class EvaluateCandidateCareerPassportReadinessHandler : IRequestHandler<EvaluateCandidateCareerPassportReadinessCommand, Response<CandidateCareerPassportReadinessDto>>
{
    private readonly ICandidateCareerPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateCandidateCareerPassportReadinessHandler(ICandidateCareerPassportReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidateCareerPassportReadinessDto>> Handle(EvaluateCandidateCareerPassportReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidateCareerPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateCareerPassportReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<CandidateCareerPassportReadinessDto>.Fail("CandidateCareerPassport readiness record was not found.", 404);
        }

        CandidateCareerPassportGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<CandidateCareerPassportReadinessDto>.Success(CandidateCareerPassportMapper.ToDto(entity));
    }
}
