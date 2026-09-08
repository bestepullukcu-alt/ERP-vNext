using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Commands;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Handlers;

public sealed class DeleteScorecardsDashboardsReadinessHandler : IRequestHandler<DeleteScorecardsDashboardsReadinessCommand, Response<bool>>
{
    private readonly IScorecardsDashboardsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteScorecardsDashboardsReadinessHandler(IScorecardsDashboardsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteScorecardsDashboardsReadinessCommand request, CancellationToken ct)
    {
        var tenant = ScorecardsDashboardsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("ScorecardsDashboards readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.ScorecardsDashboardsReadinessState = ScorecardsDashboardsReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
