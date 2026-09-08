using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Handlers;

public sealed class DeleteCompetencySkillsReadinessHandler : IRequestHandler<DeleteCompetencySkillsReadinessCommand, Response<bool>>
{
    private readonly ICompetencySkillsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteCompetencySkillsReadinessHandler(ICompetencySkillsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteCompetencySkillsReadinessCommand request, CancellationToken ct)
    {
        var tenant = CompetencySkillsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("CompetencySkills readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.CompetencySkillsReadinessState = CompetencySkillsReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
