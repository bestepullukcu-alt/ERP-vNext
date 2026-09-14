using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Handlers;

public sealed class CreateEarlyWarningSignalsReadinessHandler : IRequestHandler<CreateEarlyWarningSignalsReadinessCommand, Response<Guid>>
{
    private readonly IEarlyWarningSignalsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateEarlyWarningSignalsReadinessHandler(IEarlyWarningSignalsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateEarlyWarningSignalsReadinessCommand request, CancellationToken ct)
    {
        var tenant = EarlyWarningSignalsGuard.RequireTenant(_tenantContext);
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

        var errors = EarlyWarningSignalsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = EarlyWarningSignalsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active early-warning-signals readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = EarlyWarningSignalsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new EarlyWarningSignalsReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            EarlyWarningSignalsReadinessState = readinessState,
            SignalCatalogBoundaryState = request.Request.SignalCatalogBoundaryState,
            PatternDetectionBoundaryState = request.Request.PatternDetectionBoundaryState,
            CrossCompanyCorrelationBoundaryState = request.Request.CrossCompanyCorrelationBoundaryState,
            AlertRoutingBoundaryState = request.Request.AlertRoutingBoundaryState,
            SignalReviewBoundaryState = request.Request.SignalReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            RiskIndicatorSourceDependencyState = request.Request.RiskIndicatorSourceDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, EarlyWarningSignalsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            EarlyWarningSignalsReadinessVersion = request.Request.EarlyWarningSignalsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = EarlyWarningSignalsGuard.MergeDeferredReason(
                EarlyWarningSignalsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == EarlyWarningSignalsReadinessState.Deferred
                    ? "Early warning signals readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
