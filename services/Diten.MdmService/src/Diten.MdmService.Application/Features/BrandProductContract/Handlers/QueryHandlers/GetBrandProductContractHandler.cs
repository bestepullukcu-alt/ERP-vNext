using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BrandProductContract.Handlers.QueryHandlers;

public sealed class GetBrandProductContractHandler
    : IRequestHandler<Queries.GetBrandProductContractQuery, Response<BrandProductContractDto>>
{
    // Static capability declaration — no repository, no tenant data. The contract describes what this runtime
    // can do, not what any tenant currently holds.
    public Task<Response<BrandProductContractDto>> Handle(
        Queries.GetBrandProductContractQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Response<BrandProductContractDto>.Success(BrandProductContractFactory.Create()));
}
