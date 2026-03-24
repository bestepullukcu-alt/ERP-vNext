using Diten.MdmService.Application.Features.Countries.Requests;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Handlers;

public class BulkDeleteCountriesCommandHandler : IRequestHandler<BulkDeleteCountriesRequest, BulkDeleteResponse>
{
    private readonly ICountryRepository _repository;

    public BulkDeleteCountriesCommandHandler(ICountryRepository repository)
    {
        _repository = repository;
    }

    public async Task<BulkDeleteResponse> Handle(BulkDeleteCountriesRequest request, CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        foreach (var id in request.Ids)
        {
            await _repository.DeleteAsync(id, cancellationToken);
            deletedCount++;
        }

        return new BulkDeleteResponse { DeletedCount = deletedCount };
    }
}
