using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Handlers;

public sealed class EvaluateCompetencySkillsReadinessHandler : IRequestHandler<EvaluateCompetencySkillsReadinessCommand, Response<CompetencySkillsReadinessDto>>
{
    private readonly ICompetencySkillsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateCompetencySkillsReadinessHandler(ICompetencySkillsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CompetencySkillsReadinessDto>> Handle(EvaluateCompetencySkillsReadinessCommand request, CancellationToken ct)
    {
        var tenant = CompetencySkillsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CompetencySkillsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<CompetencySkillsReadinessDto>.Fail("CompetencySkills readiness record was not found.", 404);
        }

        CompetencySkillsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<CompetencySkillsReadinessDto>.Success(CompetencySkillsMapper.ToDto(entity));
    }
}
