using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Handlers;

public sealed class CreateIndustryTalentPoolReadinessHandler : IRequestHandler<CreateIndustryTalentPoolReadinessCommand, Response<Guid>>
{
    private readonly IIndustryTalentPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateIndustryTalentPoolReadinessHandler(IIndustryTalentPoolReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateIndustryTalentPoolReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustryTalentPoolGuard.RequireTenant(_tenantContext);
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

        var errors = IndustryTalentPoolGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = IndustryTalentPoolGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active industry-talent-pool readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = IndustryTalentPoolGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new IndustryTalentPoolReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            IndustryTalentPoolReadinessState = readinessState,
            PoolMembershipCatalogBoundaryState = request.Request.PoolMembershipCatalogBoundaryState,
            CandidateInclusionIntakeBoundaryState = request.Request.CandidateInclusionIntakeBoundaryState,
            EligibilityScopeBoundaryState = request.Request.EligibilityScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            PoolCurationReviewBoundaryState = request.Request.PoolCurationReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            ReputationSourceDependencyState = request.Request.ReputationSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EligibilityPolicyState = request.Request.EligibilityPolicyState,
            DependencyStates = new Dictionary<string, IndustryTalentPoolReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            IndustryTalentPoolReadinessVersion = request.Request.IndustryTalentPoolReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = IndustryTalentPoolGuard.MergeDeferredReason(
                IndustryTalentPoolGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == IndustryTalentPoolReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
