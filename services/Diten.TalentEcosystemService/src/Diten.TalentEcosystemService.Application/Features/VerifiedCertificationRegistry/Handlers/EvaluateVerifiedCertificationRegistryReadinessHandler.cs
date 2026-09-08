using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Handlers;

public sealed class EvaluateVerifiedCertificationRegistryReadinessHandler : IRequestHandler<EvaluateVerifiedCertificationRegistryReadinessCommand, Response<VerifiedCertificationRegistryReadinessDto>>
{
    private readonly IVerifiedCertificationRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateVerifiedCertificationRegistryReadinessHandler(IVerifiedCertificationRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<VerifiedCertificationRegistryReadinessDto>> Handle(EvaluateVerifiedCertificationRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = VerifiedCertificationRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<VerifiedCertificationRegistryReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<VerifiedCertificationRegistryReadinessDto>.Fail("VerifiedCertificationRegistry readiness record was not found.", 404);
        }

        VerifiedCertificationRegistryGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<VerifiedCertificationRegistryReadinessDto>.Success(VerifiedCertificationRegistryMapper.ToDto(entity));
    }
}
