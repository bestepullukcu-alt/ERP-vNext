using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Handlers;

public sealed class EvaluateRestrictedIntegrityRegistryReadinessHandler : IRequestHandler<EvaluateRestrictedIntegrityRegistryReadinessCommand, Response<RestrictedIntegrityRegistryReadinessDto>>
{
    private readonly IRestrictedIntegrityRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateRestrictedIntegrityRegistryReadinessHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<RestrictedIntegrityRegistryReadinessDto>> Handle(EvaluateRestrictedIntegrityRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RestrictedIntegrityRegistryReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<RestrictedIntegrityRegistryReadinessDto>.Fail("RestrictedIntegrityRegistry readiness record was not found.", 404);
        }

        RestrictedIntegrityRegistryGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<RestrictedIntegrityRegistryReadinessDto>.Success(RestrictedIntegrityRegistryMapper.ToDto(entity));
    }
}
