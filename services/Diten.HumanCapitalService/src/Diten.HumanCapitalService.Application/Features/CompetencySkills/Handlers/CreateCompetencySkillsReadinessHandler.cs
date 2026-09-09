using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Handlers;

public sealed class CreateCompetencySkillsReadinessHandler : IRequestHandler<CreateCompetencySkillsReadinessCommand, Response<Guid>>
{
    private readonly ICompetencySkillsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateCompetencySkillsReadinessHandler(ICompetencySkillsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateCompetencySkillsReadinessCommand request, CancellationToken ct)
    {
        var tenant = CompetencySkillsGuard.RequireTenant(_tenantContext);
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

        var errors = CompetencySkillsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = CompetencySkillsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active competency skills readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = CompetencySkillsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new CompetencySkillsReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            CompetencySkillsReadinessState = readinessState,
            AssessmentWorkflowBoundaryState = request.Request.AssessmentWorkflowBoundaryState,
            CompetencyFrameworkDependencyState = request.Request.CompetencyFrameworkDependencyState,
            SkillTaxonomyDependencyState = request.Request.SkillTaxonomyDependencyState,
            SkillScoringBoundaryState = request.Request.SkillScoringBoundaryState,
            RatingBoundaryState = request.Request.RatingBoundaryState,
            CalibrationBoundaryState = request.Request.CalibrationBoundaryState,
            RankingBoundaryState = request.Request.RankingBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            ManagerAssessmentUxBoundaryState = request.Request.ManagerAssessmentUxBoundaryState,
            EmployeeAssessmentUxBoundaryState = request.Request.EmployeeAssessmentUxBoundaryState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, CompetencySkillsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            CompetencySkillsReadinessVersion = request.Request.CompetencySkillsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = CompetencySkillsGuard.MergeDeferredReason(
                CompetencySkillsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == CompetencySkillsReadinessState.Deferred
                    ? "Competency skills readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
