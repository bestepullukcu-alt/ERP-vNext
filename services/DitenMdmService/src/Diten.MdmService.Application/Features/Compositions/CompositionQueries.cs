using MediatR;

namespace Diten.MdmService.Application.Features.Compositions;

public sealed record GetAllCompositionsQuery : IRequest<IReadOnlyList<CompositionListItemDto>>;

public sealed record GetCompositionByIdQuery(Guid Id, Guid? VersionId = null) : IRequest<CompositionDetailDto?>;

public sealed record GetCompositionVersionHistoryQuery(Guid CompositionId) : IRequest<IReadOnlyList<CompositionVersionSummaryDto>>;
