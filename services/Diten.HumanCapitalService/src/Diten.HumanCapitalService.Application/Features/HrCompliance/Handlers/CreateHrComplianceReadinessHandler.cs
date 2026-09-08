using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance.Handlers;

public sealed class CreateHrComplianceReadinessHandler : IRequestHandler<CreateHrComplianceReadinessCommand, Response<Guid>>
{
    private readonly IHrComplianceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateHrComplianceReadinessHandler(IHrComplianceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateHrComplianceReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrComplianceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = HrComplianceGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = HrComplianceGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active hr-compliance readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = HrComplianceGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new HrComplianceReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            HrComplianceReadinessState = readinessState,
            ObligationCatalogBoundaryState = request.Request.ObligationCatalogBoundaryState,
            ControlMappingBoundaryState = request.Request.ControlMappingBoundaryState,
            StatutoryReportDefinitionBoundaryState = request.Request.StatutoryReportDefinitionBoundaryState,
            FilingScheduleBoundaryState = request.Request.FilingScheduleBoundaryState,
            AttestationClosureBoundaryState = request.Request.AttestationClosureBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            RegulatorySourceDependencyState = request.Request.RegulatorySourceDependencyState,
            HcmDataSourceDependencyState = request.Request.HcmDataSourceDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, HrComplianceReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            HrComplianceReadinessVersion = request.Request.HrComplianceReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = HrComplianceGuard.MergeDeferredReason(
                HrComplianceGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == HrComplianceReadinessState.Deferred
                    ? "HR compliance and statutory reporting readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
