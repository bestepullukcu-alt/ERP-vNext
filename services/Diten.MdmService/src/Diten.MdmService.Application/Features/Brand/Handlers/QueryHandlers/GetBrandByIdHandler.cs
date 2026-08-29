using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.QueryHandlers;

public sealed class GetBrandByIdHandler : IRequestHandler<Queries.GetBrandByIdQuery, Response<BrandDetailDto>>
{
    private readonly IBrandRepository _repository;

    public GetBrandByIdHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BrandDetailDto>> Handle(Queries.GetBrandByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BrandId, cancellationToken);

        // Archived brands stay readable on purpose — archiving closes writes, not history.
        return entity is null
            ? BrandProductFailures.Fail<BrandDetailDto>(BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404)
            : Response<BrandDetailDto>.Success(BrandMappings.ToDetailDto(entity));
    }
}
