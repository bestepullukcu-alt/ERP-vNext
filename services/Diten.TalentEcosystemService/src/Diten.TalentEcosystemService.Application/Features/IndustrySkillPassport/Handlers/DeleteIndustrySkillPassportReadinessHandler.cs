using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Handlers;

public sealed class DeleteIndustrySkillPassportReadinessHandler : IRequestHandler<DeleteIndustrySkillPassportReadinessCommand, Response<bool>>
{
    private readonly IIndustrySkillPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteIndustrySkillPassportReadinessHandler(IIndustrySkillPassportReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteIndustrySkillPassportReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustrySkillPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("IndustrySkillPassport readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.IndustrySkillPassportReadinessState = IndustrySkillPassportReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
