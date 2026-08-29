using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Queries;

// Every filter is server-applied. The UI must not invent client-side filters for anything absent here.
public sealed record GetBrandListQuery : IRequest<Response<BrandListResultDto>>
{
    /// <summary>Free-text over BrandCode + BrandName.</summary>
    public string? Search { get; init; }

    public string? BrandStatus { get; init; }
    public Guid? BusinessUnitId { get; init; }
    public Guid? TherapeuticAreaId { get; init; }

    /// <summary>Defaults to false — archived brands stay out of the list unless explicitly requested.</summary>
    public bool IncludeArchived { get; init; }
}
