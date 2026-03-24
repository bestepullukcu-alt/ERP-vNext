using Diten.MdmService.Application.Features.Countries.Requests;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Handlers;

public class GetAllCountriesQueryHandler : IRequestHandler<GetAllCountriesQuery, IEnumerable<CountryResponse>>
{
    private readonly ICountryRepository _repository;

    public GetAllCountriesQueryHandler(ICountryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CountryResponse>> Handle(GetAllCountriesQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return entities.Select(e => new CountryResponse
        {
            Id = e.Id,
            Name = e.Name,
            Iso2Code = e.Iso2Code,
            Iso3Code = e.Iso3Code,
            PhoneCode = e.PhoneCode,
            IsActive = e.IsActive
        });
    }
}
