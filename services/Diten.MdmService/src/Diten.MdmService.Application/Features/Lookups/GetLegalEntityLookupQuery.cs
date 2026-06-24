using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Lookups;

public sealed record GetLegalEntityLookupQuery(string Type) : IRequest<Response<IReadOnlyList<LookupOptionDto>>>;

public sealed class GetLegalEntityLookupHandler
    : IRequestHandler<GetLegalEntityLookupQuery, Response<IReadOnlyList<LookupOptionDto>>>
{
    public Task<Response<IReadOnlyList<LookupOptionDto>>> Handle(GetLegalEntityLookupQuery request, CancellationToken cancellationToken)
    {
        if (!LegalEntityReferenceData.TryGet(request.Type, out var options))
        {
            return Task.FromResult(Response<IReadOnlyList<LookupOptionDto>>.Fail($"Unknown lookup type '{request.Type}'.", 404));
        }

        return Task.FromResult(Response<IReadOnlyList<LookupOptionDto>>.Success(options));
    }
}
