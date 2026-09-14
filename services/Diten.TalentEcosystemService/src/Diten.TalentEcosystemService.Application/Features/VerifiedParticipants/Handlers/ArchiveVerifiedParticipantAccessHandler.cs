using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Handlers;

public sealed class ArchiveVerifiedParticipantAccessHandler
    : IRequestHandler<ArchiveVerifiedParticipantAccessCommand, Response<NoContent>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public ArchiveVerifiedParticipantAccessHandler(ITepVerifiedParticipantAccessRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<NoContent>> Handle(ArchiveVerifiedParticipantAccessCommand request, CancellationToken ct)
    {
        var tenant = VerifiedParticipantGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Verified participant access record was not found.", 404);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.VerificationState = TepVerificationState.Archived;
        entity.AccessState = TepAccessState.Archived;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
