using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Queries;

/// <summary>Read-only brand → products relation feeding the Brand detail Products tab.</summary>
public sealed record GetBrandProductsQuery(Guid BrandId, bool IncludeArchived)
    : IRequest<Response<IReadOnlyList<BrandProductRowDto>>>;
