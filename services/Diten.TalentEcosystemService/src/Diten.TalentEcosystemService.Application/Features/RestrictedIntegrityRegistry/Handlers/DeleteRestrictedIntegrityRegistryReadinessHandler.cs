using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Handlers;

public sealed class DeleteRestrictedIntegrityRegistryReadinessHandler : IRequestHandler<DeleteRestrictedIntegrityRegistryReadinessCommand, Response<bool>>
{
    private readonly IRestrictedIntegrityRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteRestrictedIntegrityRegistryReadinessHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteRestrictedIntegrityRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("RestrictedIntegrityRegistry readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.RestrictedIntegrityRegistryReadinessState = RestrictedIntegrityRegistryReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
