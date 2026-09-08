using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Commands;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Handlers;

public sealed class EvaluateScorecardsDashboardsReadinessHandler : IRequestHandler<EvaluateScorecardsDashboardsReadinessCommand, Response<ScorecardsDashboardsReadinessDto>>
{
    private readonly IScorecardsDashboardsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateScorecardsDashboardsReadinessHandler(IScorecardsDashboardsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ScorecardsDashboardsReadinessDto>> Handle(EvaluateScorecardsDashboardsReadinessCommand request, CancellationToken ct)
    {
        var tenant = ScorecardsDashboardsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ScorecardsDashboardsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<ScorecardsDashboardsReadinessDto>.Fail("ScorecardsDashboards readiness record was not found.", 404);
        }

        ScorecardsDashboardsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<ScorecardsDashboardsReadinessDto>.Success(ScorecardsDashboardsMapper.ToDto(entity));
    }
}
