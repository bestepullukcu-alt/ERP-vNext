using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record ImportModuleCatalogCommand(
    IReadOnlyList<ModuleCatalogImportRowDto> Rows) : IRequest<ModuleCatalogImportResultDto>;
