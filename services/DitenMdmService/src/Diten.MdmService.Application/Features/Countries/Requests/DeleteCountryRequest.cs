using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Requests;

public class DeleteCountryRequest : IRequest<bool>
{
    public Guid Id { get; set; }

    public DeleteCountryRequest(Guid id)
    {
        Id = id;
    }
}
