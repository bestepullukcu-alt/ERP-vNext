using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Handlers;

public sealed class CreateIndustrySkillPassportReadinessHandler : IRequestHandler<CreateIndustrySkillPassportReadinessCommand, Response<Guid>>
{
    private readonly IIndustrySkillPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateIndustrySkillPassportReadinessHandler(IIndustrySkillPassportReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateIndustrySkillPassportReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustrySkillPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = IndustrySkillPassportGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = IndustrySkillPassportGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active industry-skill-passport readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = IndustrySkillPassportGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new IndustrySkillPassportReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            IndustrySkillPassportReadinessState = readinessState,
            SkillClaimCatalogBoundaryState = request.Request.SkillClaimCatalogBoundaryState,
            AttestationIntakeBoundaryState = request.Request.AttestationIntakeBoundaryState,
            VerificationScopeBoundaryState = request.Request.VerificationScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            PassportReviewBoundaryState = request.Request.PassportReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            CertificationSourceDependencyState = request.Request.CertificationSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            VerificationPolicyState = request.Request.VerificationPolicyState,
            DependencyStates = new Dictionary<string, IndustrySkillPassportReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            IndustrySkillPassportReadinessVersion = request.Request.IndustrySkillPassportReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = IndustrySkillPassportGuard.MergeDeferredReason(
                IndustrySkillPassportGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == IndustrySkillPassportReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
