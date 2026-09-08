using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.Succession.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.Succession.Handlers;

public sealed class CreateSuccessionReadinessHandler : IRequestHandler<CreateSuccessionReadinessCommand, Response<Guid>>
{
    private readonly ISuccessionReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateSuccessionReadinessHandler(ISuccessionReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateSuccessionReadinessCommand request, CancellationToken ct)
    {
        var tenant = SuccessionGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = SuccessionGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = SuccessionGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active succession readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = SuccessionGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new SuccessionReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            SuccessionReadinessState = readinessState,
            SuccessionPoolBoundaryState = request.Request.SuccessionPoolBoundaryState,
            HighPotentialIdentificationBoundaryState = request.Request.HighPotentialIdentificationBoundaryState,
            NominationWorkflowBoundaryState = request.Request.NominationWorkflowBoundaryState,
            ReadinessAssessmentBoundaryState = request.Request.ReadinessAssessmentBoundaryState,
            TalentReviewBoundaryState = request.Request.TalentReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentProfileDependencyState = request.Request.TalentProfileDependencyState,
            PositionFrameworkDependencyState = request.Request.PositionFrameworkDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, SuccessionReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            SuccessionReadinessVersion = request.Request.SuccessionReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = SuccessionGuard.MergeDeferredReason(
                SuccessionGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == SuccessionReadinessState.Deferred
                    ? "Succession readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
