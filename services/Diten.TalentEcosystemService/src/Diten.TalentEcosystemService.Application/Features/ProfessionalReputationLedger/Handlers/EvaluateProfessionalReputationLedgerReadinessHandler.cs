using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Handlers;

public sealed class EvaluateProfessionalReputationLedgerReadinessHandler : IRequestHandler<EvaluateProfessionalReputationLedgerReadinessCommand, Response<ProfessionalReputationLedgerReadinessDto>>
{
    private readonly IProfessionalReputationLedgerReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateProfessionalReputationLedgerReadinessHandler(IProfessionalReputationLedgerReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ProfessionalReputationLedgerReadinessDto>> Handle(EvaluateProfessionalReputationLedgerReadinessCommand request, CancellationToken ct)
    {
        var tenant = ProfessionalReputationLedgerGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ProfessionalReputationLedgerReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<ProfessionalReputationLedgerReadinessDto>.Fail("ProfessionalReputationLedger readiness record was not found.", 404);
        }

        ProfessionalReputationLedgerGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<ProfessionalReputationLedgerReadinessDto>.Success(ProfessionalReputationLedgerMapper.ToDto(entity));
    }
}
