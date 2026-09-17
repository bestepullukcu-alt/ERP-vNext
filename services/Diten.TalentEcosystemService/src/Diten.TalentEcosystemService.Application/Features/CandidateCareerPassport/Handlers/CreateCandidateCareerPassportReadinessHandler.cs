using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Handlers;

public sealed class CreateCandidateCareerPassportReadinessHandler : IRequestHandler<CreateCandidateCareerPassportReadinessCommand, Response<Guid>>
{
    private readonly ICandidateCareerPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateCandidateCareerPassportReadinessHandler(ICandidateCareerPassportReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateCandidateCareerPassportReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidateCareerPassportGuard.RequireTenant(_tenantContext);
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

        var errors = CandidateCareerPassportGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = CandidateCareerPassportGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active candidate-career-passport readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = CandidateCareerPassportGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new CandidateCareerPassportReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            CandidateCareerPassportReadinessState = readinessState,
            CareerMilestoneCatalogBoundaryState = request.Request.CareerMilestoneCatalogBoundaryState,
            ExperienceIntakeBoundaryState = request.Request.ExperienceIntakeBoundaryState,
            OwnershipScopeBoundaryState = request.Request.OwnershipScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            PassportReviewBoundaryState = request.Request.PassportReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            SkillPassportSourceDependencyState = request.Request.SkillPassportSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PortabilityPolicyState = request.Request.PortabilityPolicyState,
            DependencyStates = new Dictionary<string, CandidateCareerPassportReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            CandidateCareerPassportReadinessVersion = request.Request.CandidateCareerPassportReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = CandidateCareerPassportGuard.MergeDeferredReason(
                CandidateCareerPassportGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == CandidateCareerPassportReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
