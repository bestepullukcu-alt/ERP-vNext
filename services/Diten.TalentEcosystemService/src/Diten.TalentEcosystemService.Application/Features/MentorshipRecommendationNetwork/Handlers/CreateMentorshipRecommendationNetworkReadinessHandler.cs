using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Handlers;

public sealed class CreateMentorshipRecommendationNetworkReadinessHandler : IRequestHandler<CreateMentorshipRecommendationNetworkReadinessCommand, Response<Guid>>
{
    private readonly IMentorshipRecommendationNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateMentorshipRecommendationNetworkReadinessHandler(IMentorshipRecommendationNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateMentorshipRecommendationNetworkReadinessCommand request, CancellationToken ct)
    {
        var tenant = MentorshipRecommendationNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = MentorshipRecommendationNetworkGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = MentorshipRecommendationNetworkGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active mentorship-recommendation-network readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = MentorshipRecommendationNetworkGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new MentorshipRecommendationNetworkReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            MentorshipRecommendationNetworkReadinessState = readinessState,
            NetworkCatalogBoundaryState = request.Request.NetworkCatalogBoundaryState,
            PairingIntakeBoundaryState = request.Request.PairingIntakeBoundaryState,
            RecommendationScopeBoundaryState = request.Request.RecommendationScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            NetworkReviewBoundaryState = request.Request.NetworkReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            ReputationSourceDependencyState = request.Request.ReputationSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, MentorshipRecommendationNetworkReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            MentorshipRecommendationNetworkReadinessVersion = request.Request.MentorshipRecommendationNetworkReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = MentorshipRecommendationNetworkGuard.MergeDeferredReason(
                MentorshipRecommendationNetworkGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == MentorshipRecommendationNetworkReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
