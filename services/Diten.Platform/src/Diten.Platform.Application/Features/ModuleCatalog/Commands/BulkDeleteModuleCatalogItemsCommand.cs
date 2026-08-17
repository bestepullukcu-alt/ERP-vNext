using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record BulkDeleteModuleCatalogItemsCommand(IReadOnlyList<Guid> Ids) : IRequest<Response<NoContent>>;
