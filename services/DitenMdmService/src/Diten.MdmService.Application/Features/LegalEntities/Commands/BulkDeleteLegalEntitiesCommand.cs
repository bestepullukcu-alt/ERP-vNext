using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntities.Commands;

public sealed record BulkDeleteLegalEntitiesCommand(List<Guid> Ids) : IRequest<int>;
