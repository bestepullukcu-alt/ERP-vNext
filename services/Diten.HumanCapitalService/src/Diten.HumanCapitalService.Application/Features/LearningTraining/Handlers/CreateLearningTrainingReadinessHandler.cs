using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Handlers;

public sealed class CreateLearningTrainingReadinessHandler : IRequestHandler<CreateLearningTrainingReadinessCommand, Response<Guid>>
{
    private readonly ILearningTrainingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateLearningTrainingReadinessHandler(ILearningTrainingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateLearningTrainingReadinessCommand request, CancellationToken ct)
    {
        var tenant = LearningTrainingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = LearningTrainingGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = LearningTrainingGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active learning training readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = LearningTrainingGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new LearningTrainingReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            LearningTrainingReadinessState = readinessState,
            CourseCatalogBoundaryState = request.Request.CourseCatalogBoundaryState,
            EnrollmentWorkflowBoundaryState = request.Request.EnrollmentWorkflowBoundaryState,
            CompletionTrackingBoundaryState = request.Request.CompletionTrackingBoundaryState,
            CertificationBoundaryState = request.Request.CertificationBoundaryState,
            AssessmentScoringBoundaryState = request.Request.AssessmentScoringBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            LearningContentDependencyState = request.Request.LearningContentDependencyState,
            SkillTaxonomyDependencyState = request.Request.SkillTaxonomyDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, LearningTrainingReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            LearningTrainingReadinessVersion = request.Request.LearningTrainingReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = LearningTrainingGuard.MergeDeferredReason(
                LearningTrainingGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == LearningTrainingReadinessState.Deferred
                    ? "Learning training readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
