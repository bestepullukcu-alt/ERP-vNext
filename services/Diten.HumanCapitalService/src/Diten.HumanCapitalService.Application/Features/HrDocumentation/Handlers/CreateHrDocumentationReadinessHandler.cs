using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Handlers;

public sealed class CreateHrDocumentationReadinessHandler : IRequestHandler<CreateHrDocumentationReadinessCommand, Response<Guid>>
{
    private readonly IHrDocumentationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateHrDocumentationReadinessHandler(IHrDocumentationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateHrDocumentationReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrDocumentationGuard.RequireTenant(_tenantContext);
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

        var errors = HrDocumentationGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = HrDocumentationGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active hr-documentation readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = HrDocumentationGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new HrDocumentationReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            HrDocumentationReadinessState = readinessState,
            DocumentWorkspaceBoundaryState = request.Request.DocumentWorkspaceBoundaryState,
            EvidenceLinkBoundaryState = request.Request.EvidenceLinkBoundaryState,
            DocumentClassificationBoundaryState = request.Request.DocumentClassificationBoundaryState,
            LegalHoldBoundaryState = request.Request.LegalHoldBoundaryState,
            DispositionScheduleBoundaryState = request.Request.DispositionScheduleBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            DocumentRepositoryDependencyState = request.Request.DocumentRepositoryDependencyState,
            EvidenceStoreDependencyState = request.Request.EvidenceStoreDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, HrDocumentationReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            HrDocumentationReadinessVersion = request.Request.HrDocumentationReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = HrDocumentationGuard.MergeDeferredReason(
                HrDocumentationGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == HrDocumentationReadinessState.Deferred
                    ? "HR documentation and evidence readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
