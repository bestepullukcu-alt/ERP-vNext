using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Handlers;

public sealed class EvaluateVerifiedParticipantAccessHandler
    : IRequestHandler<EvaluateVerifiedParticipantAccessCommand, Response<VerifiedParticipantEvaluationDto>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateVerifiedParticipantAccessHandler(
        ITepVerifiedParticipantAccessRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<VerifiedParticipantEvaluationDto>> Handle(EvaluateVerifiedParticipantAccessCommand request, CancellationToken ct)
    {
        var tenant = VerifiedParticipantGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<VerifiedParticipantEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var entity = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<VerifiedParticipantEvaluationDto>.Fail("Verified participant access record was not found.", 404);
        }

        var association = entity.AssociationMembershipId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, scope, associationId, ct)
            : null;
        var policy = entity.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, scope, policyId, ct)
            : null;

        var evaluation = VerifiedParticipantGuard.Evaluate(entity, association, policy, request.Request.VerificationRequested);
        if (request.Request.VerificationRequested && !evaluation.VerificationAllowed)
        {
            return Response<VerifiedParticipantEvaluationDto>.Fail("Verified participant access evaluation failed closed.", 404);
        }

        entity.PolicyEvaluationState = evaluation.VerificationAllowed ? TepPolicyEvaluationState.Approved : TepPolicyEvaluationState.Deferred;
        entity.VisibilityApprovalState = evaluation.VerificationAllowed ? TepVisibilityApprovalState.Approved : TepVisibilityApprovalState.Deferred;
        entity.AssociationValidationState = evaluation.VerificationAllowed ? TepShellDependencyStatus.Available : TepShellDependencyStatus.Deferred;
        entity.VerifiedCompanyAccessState = evaluation.VerificationAllowed ? TepVerifiedCompanyAccessState.Ready : TepVerifiedCompanyAccessState.Deferred;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.DeferredReason = evaluation.EvaluationDeferred ? "Dependency evaluation deferred." : entity.DeferredReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<VerifiedParticipantEvaluationDto>.Success(evaluation);
    }
}
