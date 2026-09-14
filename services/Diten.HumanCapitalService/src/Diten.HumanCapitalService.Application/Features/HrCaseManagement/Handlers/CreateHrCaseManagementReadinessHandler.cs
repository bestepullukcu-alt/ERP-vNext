using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement.Handlers;

public sealed class CreateHrCaseManagementReadinessHandler : IRequestHandler<CreateHrCaseManagementReadinessCommand, Response<Guid>>
{
    private readonly IHrCaseManagementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateHrCaseManagementReadinessHandler(IHrCaseManagementReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateHrCaseManagementReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrCaseManagementGuard.RequireTenant(_tenantContext);
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

        var errors = HrCaseManagementGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = HrCaseManagementGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active hr-case-management readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = HrCaseManagementGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new HrCaseManagementReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            HrCaseManagementReadinessState = readinessState,
            CaseIntakeBoundaryState = request.Request.CaseIntakeBoundaryState,
            CaseTriageBoundaryState = request.Request.CaseTriageBoundaryState,
            InvestigationTrackingBoundaryState = request.Request.InvestigationTrackingBoundaryState,
            DisciplinaryActionBoundaryState = request.Request.DisciplinaryActionBoundaryState,
            ResolutionClosureBoundaryState = request.Request.ResolutionClosureBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            EmployeeRecordDependencyState = request.Request.EmployeeRecordDependencyState,
            SensitiveAccessPolicyDependencyState = request.Request.SensitiveAccessPolicyDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, HrCaseManagementReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            HrCaseManagementReadinessVersion = request.Request.HrCaseManagementReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = HrCaseManagementGuard.MergeDeferredReason(
                HrCaseManagementGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == HrCaseManagementReadinessState.Deferred
                    ? "Employee relations and HR case readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
