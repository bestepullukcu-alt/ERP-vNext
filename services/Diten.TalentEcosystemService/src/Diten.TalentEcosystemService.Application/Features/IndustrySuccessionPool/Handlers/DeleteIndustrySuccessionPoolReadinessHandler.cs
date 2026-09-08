using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Handlers;

public sealed class DeleteIndustrySuccessionPoolReadinessHandler : IRequestHandler<DeleteIndustrySuccessionPoolReadinessCommand, Response<bool>>
{
    private readonly IIndustrySuccessionPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteIndustrySuccessionPoolReadinessHandler(IIndustrySuccessionPoolReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteIndustrySuccessionPoolReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustrySuccessionPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("IndustrySuccessionPool readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.IndustrySuccessionPoolReadinessState = IndustrySuccessionPoolReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
