using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Handlers;

public sealed class GetAssociationMembershipListHandler
    : IRequestHandler<GetAssociationMembershipListQuery, Response<IReadOnlyList<AssociationMembershipRegistryListItemDto>>>
{
    private readonly ITepAssociationMembershipRegistryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetAssociationMembershipListHandler(
        ITepAssociationMembershipRegistryRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<AssociationMembershipRegistryListItemDto>>> Handle(
        GetAssociationMembershipListQuery request,
        CancellationToken ct)
    {
        var tenant = AssociationMembershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<AssociationMembershipRegistryListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var records = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<AssociationMembershipRegistryListItemDto>>.Success(
            records.Select(AssociationMembershipMapper.ToListItemDto).ToList());
    }
}

public sealed class GetAssociationMembershipByIdHandler
    : IRequestHandler<GetAssociationMembershipByIdQuery, Response<AssociationMembershipRegistryDto>>
{
    private readonly ITepAssociationMembershipRegistryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetAssociationMembershipByIdHandler(
        ITepAssociationMembershipRegistryRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AssociationMembershipRegistryDto>> Handle(GetAssociationMembershipByIdQuery request, CancellationToken ct)
    {
        var tenant = AssociationMembershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationMembershipRegistryDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<AssociationMembershipRegistryDto>.Fail("Association membership registry record was not found.", 404)
            : Response<AssociationMembershipRegistryDto>.Success(AssociationMembershipMapper.ToDto(entity));
    }
}
