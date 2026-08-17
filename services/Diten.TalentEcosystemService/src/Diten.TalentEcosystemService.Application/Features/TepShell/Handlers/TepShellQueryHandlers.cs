using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TepShell.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Handlers;

public sealed class GetTepShellMetadataListHandler
    : IRequestHandler<GetTepShellMetadataListQuery, Response<IReadOnlyList<TepShellMetadataListItemDto>>>
{
    private readonly ITepShellMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTepShellMetadataListHandler(ITepShellMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<TepShellMetadataListItemDto>>> Handle(GetTepShellMetadataListQuery request, CancellationToken ct)
    {
        var tenant = TepShellGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<TepShellMetadataListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var items = await _repository.ListAsync(tenantId, ct);
        return Response<IReadOnlyList<TepShellMetadataListItemDto>>.Success(items.Select(TepShellMapper.ToListItemDto).ToList());
    }
}

public sealed class GetTepShellMetadataByIdHandler
    : IRequestHandler<GetTepShellMetadataByIdQuery, Response<TepShellMetadataDto>>
{
    private readonly ITepShellMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTepShellMetadataByIdHandler(ITepShellMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TepShellMetadataDto>> Handle(GetTepShellMetadataByIdQuery request, CancellationToken ct)
    {
        var tenant = TepShellGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TepShellMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var item = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        return item is null
            ? Response<TepShellMetadataDto>.Fail("TEP shell metadata was not found.", 404)
            : Response<TepShellMetadataDto>.Success(TepShellMapper.ToDto(item));
    }
}

public sealed class GetTepShellHealthHandler
    : IRequestHandler<GetTepShellHealthQuery, Response<TepShellHealthDto>>
{
    public Task<Response<TepShellHealthDto>> Handle(GetTepShellHealthQuery request, CancellationToken ct) =>
        Task.FromResult(Response<TepShellHealthDto>.Success(new TepShellHealthDto(TepShellPermissions.RuntimeOwnerKey, "Healthy")));
}
