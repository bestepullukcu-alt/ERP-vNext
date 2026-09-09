using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Handlers;

public sealed class CreateHeadcountBudgetReadinessHandler : IRequestHandler<CreateHeadcountBudgetReadinessCommand, Response<Guid>>
{
    private readonly IHeadcountBudgetReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateHeadcountBudgetReadinessHandler(IHeadcountBudgetReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateHeadcountBudgetReadinessCommand request, CancellationToken ct)
    {
        var tenant = HeadcountBudgetGuard.RequireTenant(_tenantContext);
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

        var errors = HeadcountBudgetGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = HeadcountBudgetGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active headcount-budget readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = HeadcountBudgetGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new HeadcountBudgetReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            HeadcountBudgetReadinessState = readinessState,
            HeadcountRequisitionBoundaryState = request.Request.HeadcountRequisitionBoundaryState,
            PositionBudgetBoundaryState = request.Request.PositionBudgetBoundaryState,
            BudgetAllocationBoundaryState = request.Request.BudgetAllocationBoundaryState,
            BudgetApprovalBoundaryState = request.Request.BudgetApprovalBoundaryState,
            BudgetReconciliationBoundaryState = request.Request.BudgetReconciliationBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            OrganizationStructureDependencyState = request.Request.OrganizationStructureDependencyState,
            PositionFrameworkDependencyState = request.Request.PositionFrameworkDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, HeadcountBudgetReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            HeadcountBudgetReadinessVersion = request.Request.HeadcountBudgetReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = HeadcountBudgetGuard.MergeDeferredReason(
                HeadcountBudgetGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == HeadcountBudgetReadinessState.Deferred
                    ? "Headcount and position budget readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
