using Diten.MdmService.Application.Features.Countries.Requests;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Handlers;

public class DeleteCountryCommandHandler : IRequestHandler<DeleteCountryRequest, bool>
{
    private readonly ICountryRepository _repository;

    public DeleteCountryCommandHandler(ICountryRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteCountryRequest request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}
