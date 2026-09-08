using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Handlers;

public sealed class DeleteIndustryTalentPoolReadinessHandler : IRequestHandler<DeleteIndustryTalentPoolReadinessCommand, Response<bool>>
{
    private readonly IIndustryTalentPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteIndustryTalentPoolReadinessHandler(IIndustryTalentPoolReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteIndustryTalentPoolReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustryTalentPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("IndustryTalentPool readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.IndustryTalentPoolReadinessState = IndustryTalentPoolReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
