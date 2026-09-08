using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Handlers;

public sealed class CreateSectorMobilityIntelligenceReadinessHandler : IRequestHandler<CreateSectorMobilityIntelligenceReadinessCommand, Response<Guid>>
{
    private readonly ISectorMobilityIntelligenceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateSectorMobilityIntelligenceReadinessHandler(ISectorMobilityIntelligenceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateSectorMobilityIntelligenceReadinessCommand request, CancellationToken ct)
    {
        var tenant = SectorMobilityIntelligenceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = SectorMobilityIntelligenceGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = SectorMobilityIntelligenceGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active sector-mobility-intelligence readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = SectorMobilityIntelligenceGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new SectorMobilityIntelligenceReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            SectorMobilityIntelligenceReadinessState = readinessState,
            MobilityCatalogBoundaryState = request.Request.MobilityCatalogBoundaryState,
            FlowBindingIntakeBoundaryState = request.Request.FlowBindingIntakeBoundaryState,
            CorridorScopeBoundaryState = request.Request.CorridorScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            MobilityReviewBoundaryState = request.Request.MobilityReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            SectorTrendSourceDependencyState = request.Request.SectorTrendSourceDependencyState,
            WorkforceAnalyticsSourceDependencyState = request.Request.WorkforceAnalyticsSourceDependencyState,
            SkillsTaxonomySourceDependencyState = request.Request.SkillsTaxonomySourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, SectorMobilityIntelligenceReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            SectorMobilityIntelligenceReadinessVersion = request.Request.SectorMobilityIntelligenceReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = SectorMobilityIntelligenceGuard.MergeDeferredReason(
                SectorMobilityIntelligenceGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == SectorMobilityIntelligenceReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
