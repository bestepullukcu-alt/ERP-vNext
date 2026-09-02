using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Handlers;

public sealed class ArchiveCandidateDisputeReadinessHandler : IRequestHandler<ArchiveCandidateDisputeReadinessCommand, Response<NoContent>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public ArchiveCandidateDisputeReadinessHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ArchiveCandidateDisputeReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Candidate dispute readiness record was not found.", 404);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DisputeReadinessState = TepCandidateDisputeReadinessState.Archived;
        entity.UpdatedAt = entity.DeletedAt;
        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
