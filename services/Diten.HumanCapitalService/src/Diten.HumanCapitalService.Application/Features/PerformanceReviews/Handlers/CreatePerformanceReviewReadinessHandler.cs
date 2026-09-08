using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Handlers;

public sealed class CreatePerformanceReviewReadinessHandler : IRequestHandler<CreatePerformanceReviewReadinessCommand, Response<Guid>>
{
    private readonly IPerformanceReviewReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreatePerformanceReviewReadinessHandler(IPerformanceReviewReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreatePerformanceReviewReadinessCommand request, CancellationToken ct)
    {
        var tenant = PerformanceReviewGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = PerformanceReviewGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = PerformanceReviewGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active performance review readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = PerformanceReviewGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new PerformanceReviewReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            PerformanceReviewReadinessState = readinessState,
            ReviewCycleBoundaryState = request.Request.ReviewCycleBoundaryState,
            GoalDependencyState = request.Request.GoalDependencyState,
            ScoringBoundaryState = request.Request.ScoringBoundaryState,
            RatingBoundaryState = request.Request.RatingBoundaryState,
            CalibrationBoundaryState = request.Request.CalibrationBoundaryState,
            RankingBoundaryState = request.Request.RankingBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            ManagerReviewUxBoundaryState = request.Request.ManagerReviewUxBoundaryState,
            EmployeeReviewUxBoundaryState = request.Request.EmployeeReviewUxBoundaryState,
            CompensationDataBoundaryState = request.Request.CompensationDataBoundaryState,
            BenefitsDataBoundaryState = request.Request.BenefitsDataBoundaryState,
            PayrollDataBoundaryState = request.Request.PayrollDataBoundaryState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, PerformanceReviewReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            PerformanceReviewReadinessVersion = request.Request.PerformanceReviewReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = PerformanceReviewGuard.MergeDeferredReason(
                PerformanceReviewGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == PerformanceReviewReadinessState.Deferred
                    ? "Performance review readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
