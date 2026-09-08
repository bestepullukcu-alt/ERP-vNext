using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Handlers;

public sealed class CreateSkillsGapHeatmapReadinessHandler : IRequestHandler<CreateSkillsGapHeatmapReadinessCommand, Response<Guid>>
{
    private readonly ISkillsGapHeatmapReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateSkillsGapHeatmapReadinessHandler(ISkillsGapHeatmapReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateSkillsGapHeatmapReadinessCommand request, CancellationToken ct)
    {
        var tenant = SkillsGapHeatmapGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = SkillsGapHeatmapGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = SkillsGapHeatmapGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active skills-gap-heatmap readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = SkillsGapHeatmapGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new SkillsGapHeatmapReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            SkillsGapHeatmapReadinessState = readinessState,
            GapCatalogBoundaryState = request.Request.GapCatalogBoundaryState,
            HeatmapBindingIntakeBoundaryState = request.Request.HeatmapBindingIntakeBoundaryState,
            SeverityScopeBoundaryState = request.Request.SeverityScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            GapReviewBoundaryState = request.Request.GapReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            SkillsTaxonomySourceDependencyState = request.Request.SkillsTaxonomySourceDependencyState,
            WorkforceAnalyticsSourceDependencyState = request.Request.WorkforceAnalyticsSourceDependencyState,
            TalentDemandForecastSourceDependencyState = request.Request.TalentDemandForecastSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, SkillsGapHeatmapReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            SkillsGapHeatmapReadinessVersion = request.Request.SkillsGapHeatmapReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = SkillsGapHeatmapGuard.MergeDeferredReason(
                SkillsGapHeatmapGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == SkillsGapHeatmapReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
