using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Requests;

public class CreateCountryRequest : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Iso2Code { get; set; } = string.Empty;
    public string Iso3Code { get; set; } = string.Empty;
    public string? PhoneCode { get; set; }
    public bool IsActive { get; set; } = true;
}
