using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Queries;
using Diten.Platform.Application.Features.Lookups.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Lookups.Handlers.QueryHandlers;

public sealed class GetTenantTierLookupHandler
    : IRequestHandler<GetTenantTierLookupQuery, Response<IReadOnlyList<LookupOptionDto>>>
{
    private readonly IPlatformLookupProvider _provider;

    public GetTenantTierLookupHandler(IPlatformLookupProvider provider)
    {
        _provider = provider;
    }

    public async Task<Response<IReadOnlyList<LookupOptionDto>>> Handle(GetTenantTierLookupQuery request, CancellationToken ct)
    {
        var options = await _provider.GetTenantTiersAsync(ct);
        return Response<IReadOnlyList<LookupOptionDto>>.Success(options);
    }
}
