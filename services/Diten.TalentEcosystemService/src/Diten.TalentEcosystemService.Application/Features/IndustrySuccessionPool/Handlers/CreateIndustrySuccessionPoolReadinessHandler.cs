using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Handlers;

public sealed class CreateIndustrySuccessionPoolReadinessHandler : IRequestHandler<CreateIndustrySuccessionPoolReadinessCommand, Response<Guid>>
{
    private readonly IIndustrySuccessionPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateIndustrySuccessionPoolReadinessHandler(IIndustrySuccessionPoolReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateIndustrySuccessionPoolReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustrySuccessionPoolGuard.RequireTenant(_tenantContext);
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

        var errors = IndustrySuccessionPoolGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = IndustrySuccessionPoolGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active industry-succession-pool readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = IndustrySuccessionPoolGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new IndustrySuccessionPoolReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            IndustrySuccessionPoolReadinessState = readinessState,
            PoolCatalogBoundaryState = request.Request.PoolCatalogBoundaryState,
            CandidateInclusionIntakeBoundaryState = request.Request.CandidateInclusionIntakeBoundaryState,
            ReadinessTierScopeBoundaryState = request.Request.ReadinessTierScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            SuccessionReviewBoundaryState = request.Request.SuccessionReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            TalentPoolSourceDependencyState = request.Request.TalentPoolSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, IndustrySuccessionPoolReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            IndustrySuccessionPoolReadinessVersion = request.Request.IndustrySuccessionPoolReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = IndustrySuccessionPoolGuard.MergeDeferredReason(
                IndustrySuccessionPoolGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == IndustrySuccessionPoolReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
