using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>The closed attribute catalog, published exactly as the runtime enforces it: same codes, same operators,
/// same required parameters. A criteria editor is built from THIS and never from a hardcoded list.</summary>
public sealed record GetSegmentAttributeCatalogQuery : IRequest<Response<SegmentAttributeCatalogDto>>;
