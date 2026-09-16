using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Handlers;

public sealed class ArchiveConsentVisibilityPolicyHandler
    : IRequestHandler<ArchiveConsentVisibilityPolicyCommand, Response<NoContent>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public ArchiveConsentVisibilityPolicyHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<NoContent>> Handle(ArchiveConsentVisibilityPolicyCommand request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Consent/visibility policy was not found.", 404);
        }

        entity.PolicyState = TepPolicyState.Archived;
        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = entity.DeletedAt;

        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
