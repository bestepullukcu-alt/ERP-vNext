using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Queries;
using Diten.Platform.Application.Features.Lookups.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Lookups.Handlers.QueryHandlers;

public sealed class GetLocaleLookupHandler
    : IRequestHandler<GetLocaleLookupQuery, Response<IReadOnlyList<LookupOptionDto>>>
{
    private readonly IPlatformLookupProvider _provider;

    public GetLocaleLookupHandler(IPlatformLookupProvider provider)
    {
        _provider = provider;
    }

    public async Task<Response<IReadOnlyList<LookupOptionDto>>> Handle(GetLocaleLookupQuery request, CancellationToken ct)
    {
        var options = await _provider.GetLocalesAsync(ct);
        return Response<IReadOnlyList<LookupOptionDto>>.Success(options);
    }
}
