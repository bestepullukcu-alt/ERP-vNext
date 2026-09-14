using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Handlers;

public sealed class ArchiveExitReferenceRecordHandler : IRequestHandler<ArchiveExitReferenceRecordCommand, Response<NoContent>>
{
    private readonly ITepExitReferenceRecordMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public ArchiveExitReferenceRecordHandler(ITepExitReferenceRecordMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<NoContent>> Handle(ArchiveExitReferenceRecordCommand request, CancellationToken ct)
    {
        var tenant = ExitReferenceRecordGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Exit reference record was not found.", 404);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.ReferenceRecordState = TepExitReferenceRecordState.Archived;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        return Response<NoContent>.Success(204);
    }
}
