using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Handlers;

public sealed class UpdateAssociationMemberCompanyHandler
    : IRequestHandler<UpdateAssociationMemberCompanyCommand, Response<AssociationMembershipRegistryDto>>
{
    private readonly ITepAssociationMembershipRegistryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public UpdateAssociationMemberCompanyHandler(
        ITepAssociationMembershipRegistryRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<AssociationMembershipRegistryDto>> Handle(UpdateAssociationMemberCompanyCommand request, CancellationToken ct)
    {
        var tenant = AssociationMembershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationMembershipRegistryDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<AssociationMembershipRegistryDto>.Fail("Association membership registry record was not found.", 404);
        }

        var validation = AssociationMembershipGuard.ValidateMemberCompanyRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<AssociationMembershipRegistryDto>.Fail(validation, 400);
        }

        entity.MemberCompanyState = request.Request.MemberCompanyState;
        entity.MemberCompanyReference = request.Request.MemberCompanyReference.Trim();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<AssociationMembershipRegistryDto>.Success(AssociationMembershipMapper.ToDto(entity));
    }
}
