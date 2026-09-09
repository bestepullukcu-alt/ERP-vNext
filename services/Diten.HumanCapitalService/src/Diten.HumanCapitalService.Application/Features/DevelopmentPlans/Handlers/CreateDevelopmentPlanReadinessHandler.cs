using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Handlers;

public sealed class CreateDevelopmentPlanReadinessHandler : IRequestHandler<CreateDevelopmentPlanReadinessCommand, Response<Guid>>
{
    private readonly IDevelopmentPlanReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateDevelopmentPlanReadinessHandler(IDevelopmentPlanReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateDevelopmentPlanReadinessCommand request, CancellationToken ct)
    {
        var tenant = DevelopmentPlanGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        if (!await _legalEntityContext.IsSelectionAllowedAsync(ct))
        {
            return Response<Guid>.Fail(
                "A permitted legal entity must be selected (X-Legal-Entity-Id) to create this record.",
                403);
        }

        var errors = DevelopmentPlanGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = DevelopmentPlanGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active development plan readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = DevelopmentPlanGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new DevelopmentPlanReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            DevelopmentPlanReadinessState = readinessState,
            DevelopmentPlanWorkflowBoundaryState = request.Request.DevelopmentPlanWorkflowBoundaryState,
            GoalAssignmentBoundaryState = request.Request.GoalAssignmentBoundaryState,
            LearningAssignmentBoundaryState = request.Request.LearningAssignmentBoundaryState,
            SkillGapScoringBoundaryState = request.Request.SkillGapScoringBoundaryState,
            RatingBoundaryState = request.Request.RatingBoundaryState,
            RecommendationBoundaryState = request.Request.RecommendationBoundaryState,
            RankingBoundaryState = request.Request.RankingBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            ManagerActionUxBoundaryState = request.Request.ManagerActionUxBoundaryState,
            CoachingActionBoundaryState = request.Request.CoachingActionBoundaryState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, DevelopmentPlanReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            DevelopmentPlanReadinessVersion = request.Request.DevelopmentPlanReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = DevelopmentPlanGuard.MergeDeferredReason(
                DevelopmentPlanGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == DevelopmentPlanReadinessState.Deferred
                    ? "Development plan readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
