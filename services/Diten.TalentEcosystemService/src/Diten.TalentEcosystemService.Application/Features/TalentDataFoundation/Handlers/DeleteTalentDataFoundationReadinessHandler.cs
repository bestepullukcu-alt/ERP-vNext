using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Handlers;

public sealed class DeleteTalentDataFoundationReadinessHandler : IRequestHandler<DeleteTalentDataFoundationReadinessCommand, Response<bool>>
{
    private readonly ITalentDataFoundationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteTalentDataFoundationReadinessHandler(ITalentDataFoundationReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteTalentDataFoundationReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentDataFoundationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("TalentDataFoundation readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.TalentDataFoundationReadinessState = TalentDataFoundationReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
