namespace Diten.MdmService.Application.Features.Countries.Requests;

public sealed record BulkDeleteCountriesRequest(List<Guid> Ids);

