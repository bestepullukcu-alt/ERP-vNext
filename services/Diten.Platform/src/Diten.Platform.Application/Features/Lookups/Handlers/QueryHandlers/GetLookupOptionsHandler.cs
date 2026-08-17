using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Queries;
using Diten.Platform.Application.Features.Lookups.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Lookups.Handlers.QueryHandlers;

public sealed class GetLookupOptionsHandler
    : IRequestHandler<GetLookupOptionsQuery, Response<IReadOnlyList<LookupOptionDto>>>
{
    private readonly IPlatformLookupProvider _provider;

    public GetLookupOptionsHandler(IPlatformLookupProvider provider)
    {
        _provider = provider;
    }

    public async Task<Response<IReadOnlyList<LookupOptionDto>>> Handle(GetLookupOptionsQuery request, CancellationToken ct)
    {
        var options = await _provider.GetLookupOptionsAsync(request.LookupKey, ct);
        return options is null
            ? Response<IReadOnlyList<LookupOptionDto>>.Fail("Lookup key was not found.", 404)
            : Response<IReadOnlyList<LookupOptionDto>>.Success(options);
    }
}
