using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Handlers;

public sealed class EvaluateIndustrySkillPassportReadinessHandler : IRequestHandler<EvaluateIndustrySkillPassportReadinessCommand, Response<IndustrySkillPassportReadinessDto>>
{
    private readonly IIndustrySkillPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateIndustrySkillPassportReadinessHandler(IIndustrySkillPassportReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IndustrySkillPassportReadinessDto>> Handle(EvaluateIndustrySkillPassportReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustrySkillPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustrySkillPassportReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<IndustrySkillPassportReadinessDto>.Fail("IndustrySkillPassport readiness record was not found.", 404);
        }

        IndustrySkillPassportGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<IndustrySkillPassportReadinessDto>.Success(IndustrySkillPassportMapper.ToDto(entity));
    }
}
