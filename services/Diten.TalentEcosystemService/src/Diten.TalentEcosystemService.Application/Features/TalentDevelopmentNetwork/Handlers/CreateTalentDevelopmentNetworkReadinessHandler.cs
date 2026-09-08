using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Handlers;

public sealed class CreateTalentDevelopmentNetworkReadinessHandler : IRequestHandler<CreateTalentDevelopmentNetworkReadinessCommand, Response<Guid>>
{
    private readonly ITalentDevelopmentNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateTalentDevelopmentNetworkReadinessHandler(ITalentDevelopmentNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateTalentDevelopmentNetworkReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentDevelopmentNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = TalentDevelopmentNetworkGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = TalentDevelopmentNetworkGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active talent-development-network readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = TalentDevelopmentNetworkGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new TalentDevelopmentNetworkReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            TalentDevelopmentNetworkReadinessState = readinessState,
            PathwayCatalogBoundaryState = request.Request.PathwayCatalogBoundaryState,
            MentorshipLinkIntakeBoundaryState = request.Request.MentorshipLinkIntakeBoundaryState,
            ProgressionScopeBoundaryState = request.Request.ProgressionScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            NetworkReviewBoundaryState = request.Request.NetworkReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            SkillPassportSourceDependencyState = request.Request.SkillPassportSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            ProgressionPolicyState = request.Request.ProgressionPolicyState,
            DependencyStates = new Dictionary<string, TalentDevelopmentNetworkReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            TalentDevelopmentNetworkReadinessVersion = request.Request.TalentDevelopmentNetworkReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = TalentDevelopmentNetworkGuard.MergeDeferredReason(
                TalentDevelopmentNetworkGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == TalentDevelopmentNetworkReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
