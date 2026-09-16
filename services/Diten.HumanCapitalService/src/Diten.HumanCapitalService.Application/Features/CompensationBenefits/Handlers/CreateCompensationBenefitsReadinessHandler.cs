using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits.Handlers;

public sealed class CreateCompensationBenefitsReadinessHandler : IRequestHandler<CreateCompensationBenefitsReadinessCommand, Response<Guid>>
{
    private readonly ICompensationBenefitsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateCompensationBenefitsReadinessHandler(ICompensationBenefitsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateCompensationBenefitsReadinessCommand request, CancellationToken ct)
    {
        var tenant = CompensationBenefitsGuard.RequireTenant(_tenantContext);
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

        var errors = CompensationBenefitsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = CompensationBenefitsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active compensation-benefits readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = CompensationBenefitsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new CompensationBenefitsReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            CompensationBenefitsReadinessState = readinessState,
            CompensationPlanBoundaryState = request.Request.CompensationPlanBoundaryState,
            BenefitProgramBoundaryState = request.Request.BenefitProgramBoundaryState,
            PayGradeMappingBoundaryState = request.Request.PayGradeMappingBoundaryState,
            BenefitEnrollmentBoundaryState = request.Request.BenefitEnrollmentBoundaryState,
            CompensationReviewBoundaryState = request.Request.CompensationReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            CompensationSourceDependencyState = request.Request.CompensationSourceDependencyState,
            BenefitProviderSourceDependencyState = request.Request.BenefitProviderSourceDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, CompensationBenefitsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            CompensationBenefitsReadinessVersion = request.Request.CompensationBenefitsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = CompensationBenefitsGuard.MergeDeferredReason(
                CompensationBenefitsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == CompensationBenefitsReadinessState.Deferred
                    ? "Compensation and benefits readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
