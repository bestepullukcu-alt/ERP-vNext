using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Requests;

public class BulkDeleteCountriesRequest : IRequest<BulkDeleteResponse>
{
    public List<Guid> Ids { get; set; } = new();
}

public class BulkDeleteResponse
{
    public int DeletedCount { get; set; }
}
