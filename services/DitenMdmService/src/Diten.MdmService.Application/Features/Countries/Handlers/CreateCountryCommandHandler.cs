using Diten.MdmService.Application.Features.Countries.Requests;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Handlers;

public class CreateCountryCommandHandler : IRequestHandler<CreateCountryRequest, Guid>
{
    private readonly ICountryRepository _repository;

    public CreateCountryCommandHandler(ICountryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateCountryRequest request, CancellationToken cancellationToken)
    {
        var country = new Country
        {
            Name = request.Name.Trim(),
            Iso2Code = request.Iso2Code.Trim().ToUpperInvariant(),
            Iso3Code = request.Iso3Code.Trim().ToUpperInvariant(),
            PhoneCode = request.PhoneCode?.Trim(),
            IsActive = request.IsActive
        };

        if (await _repository.ExistsByIso2Async(country.Iso2Code, cancellationToken))
        {
            throw new Exception("Country with this ISO2 code already exists.");
        }

        await _repository.CreateAsync(country, cancellationToken);
        return country.Id;
    }
}
