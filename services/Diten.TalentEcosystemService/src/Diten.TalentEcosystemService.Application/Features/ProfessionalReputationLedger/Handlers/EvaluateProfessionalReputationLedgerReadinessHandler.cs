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

    public EvaluateProfessionalReputationLedgerReadinessHandler(IProfessionalReputationLedgerReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ProfessionalReputationLedgerReadinessDto>> Handle(EvaluateProfessionalReputationLedgerReadinessCommand request, CancellationToken ct)
    {
        var tenant = ProfessionalReputationLedgerGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ProfessionalReputationLedgerReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<ProfessionalReputationLedgerReadinessDto>.Fail("ProfessionalReputationLedger readiness record was not found.", 404);
        }

        ProfessionalReputationLedgerGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<ProfessionalReputationLedgerReadinessDto>.Success(ProfessionalReputationLedgerMapper.ToDto(entity));
    }
}
