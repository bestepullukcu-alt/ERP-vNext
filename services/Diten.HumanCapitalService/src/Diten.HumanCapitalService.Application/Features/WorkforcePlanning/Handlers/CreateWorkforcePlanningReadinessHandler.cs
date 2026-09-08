using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Handlers;

public sealed class CreateWorkforcePlanningReadinessHandler : IRequestHandler<CreateWorkforcePlanningReadinessCommand, Response<Guid>>
{
    private readonly IWorkforcePlanningReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateWorkforcePlanningReadinessHandler(IWorkforcePlanningReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateWorkforcePlanningReadinessCommand request, CancellationToken ct)
    {
        var tenant = WorkforcePlanningGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = WorkforcePlanningGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = WorkforcePlanningGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active workforce-planning readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = WorkforcePlanningGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new WorkforcePlanningReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            WorkforcePlanningReadinessState = readinessState,
            HeadcountPlanBoundaryState = request.Request.HeadcountPlanBoundaryState,
            DemandForecastBoundaryState = request.Request.DemandForecastBoundaryState,
            SupplyForecastBoundaryState = request.Request.SupplyForecastBoundaryState,
            GapAnalysisBoundaryState = request.Request.GapAnalysisBoundaryState,
            ScenarioModelingBoundaryState = request.Request.ScenarioModelingBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            OrganizationStructureDependencyState = request.Request.OrganizationStructureDependencyState,
            PositionFrameworkDependencyState = request.Request.PositionFrameworkDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, WorkforcePlanningReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            WorkforcePlanningReadinessVersion = request.Request.WorkforcePlanningReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = WorkforcePlanningGuard.MergeDeferredReason(
                WorkforcePlanningGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == WorkforcePlanningReadinessState.Deferred
                    ? "Workforce planning readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
