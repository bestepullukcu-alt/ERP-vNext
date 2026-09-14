using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Handlers;

public sealed class CreateProfessionalReputationLedgerReadinessHandler : IRequestHandler<CreateProfessionalReputationLedgerReadinessCommand, Response<Guid>>
{
    private readonly IProfessionalReputationLedgerReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateProfessionalReputationLedgerReadinessHandler(IProfessionalReputationLedgerReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateProfessionalReputationLedgerReadinessCommand request, CancellationToken ct)
    {
        var tenant = ProfessionalReputationLedgerGuard.RequireTenant(_tenantContext);
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

        var errors = ProfessionalReputationLedgerGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = ProfessionalReputationLedgerGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active professional-reputation-ledger readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = ProfessionalReputationLedgerGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new ProfessionalReputationLedgerReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            ProfessionalReputationLedgerReadinessState = readinessState,
            ReputationSignalCatalogBoundaryState = request.Request.ReputationSignalCatalogBoundaryState,
            EndorsementIntakeBoundaryState = request.Request.EndorsementIntakeBoundaryState,
            AttributionScopeBoundaryState = request.Request.AttributionScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            SignalReviewBoundaryState = request.Request.SignalReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, ProfessionalReputationLedgerReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            ProfessionalReputationLedgerReadinessVersion = request.Request.ProfessionalReputationLedgerReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = ProfessionalReputationLedgerGuard.MergeDeferredReason(
                ProfessionalReputationLedgerGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == ProfessionalReputationLedgerReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
