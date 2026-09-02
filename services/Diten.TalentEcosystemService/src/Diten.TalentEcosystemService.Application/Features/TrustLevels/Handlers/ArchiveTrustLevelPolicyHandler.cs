using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Handlers;

public sealed class ArchiveTrustLevelPolicyHandler : IRequestHandler<ArchiveTrustLevelPolicyCommand, Response<NoContent>>
{
    private readonly ITepTrustLevelPolicyMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public ArchiveTrustLevelPolicyHandler(ITepTrustLevelPolicyMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ArchiveTrustLevelPolicyCommand request, CancellationToken ct)
    {
        var tenant = TrustLevelGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Trust-level policy was not found.", 404);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.TrustLevelPolicyState = TepTrustLevelPolicyState.Archived;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
