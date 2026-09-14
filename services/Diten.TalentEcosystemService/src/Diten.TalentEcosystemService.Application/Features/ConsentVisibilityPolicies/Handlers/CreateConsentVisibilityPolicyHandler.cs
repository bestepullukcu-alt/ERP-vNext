using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Handlers;

public sealed class CreateConsentVisibilityPolicyHandler
    : IRequestHandler<CreateConsentVisibilityPolicyCommand, Response<Guid>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateConsentVisibilityPolicyHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateConsentVisibilityPolicyCommand request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        if (!await _legalEntityContext.IsSelectionAllowedAsync(ct))
        {
            return Response<Guid>.Fail(
                "A permitted legal entity must be selected (X-Legal-Entity-Id) to create this record.",
                403);
        }

        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;

        var validation = ConsentVisibilityPolicyGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var activation = ConsentVisibilityPolicyGuard.ValidateActivationRules(request.Request);
        if (!activation.IsSuccessful)
        {
            return Response<Guid>.Fail(activation.Errors, activation.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active consent/visibility policy with the same Code already exists for this tenant.", 409);
        }

        var entity = new TepConsentVisibilityPolicy
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = request.Request.Code.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            PolicyState = request.Request.PolicyState,
            ConsentRequirementState = request.Request.ConsentRequirementState,
            VisibilityScope = request.Request.VisibilityScope,
            DataScopeState = request.Request.DataScopeState,
            AccessPolicyState = request.Request.AccessPolicyState,
            AssociationConsumptionState = request.Request.AssociationConsumptionState,
            PolicyUnavailableBehavior = request.Request.PolicyUnavailableBehavior,
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            DependencyStates = request.Request.DependencyStates.Select(ConsentVisibilityPolicyMapper.ToEntity).ToList(),
            LocalAuditEvidenceRetentionState = request.Request.LocalAuditEvidenceRetentionState,
            LastEvaluatedAt = request.Request.LastEvaluatedAt,
            PolicyVersion = request.Request.PolicyVersion
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
