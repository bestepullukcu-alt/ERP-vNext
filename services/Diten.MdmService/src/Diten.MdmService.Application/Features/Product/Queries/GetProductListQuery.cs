using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Queries;

// Every filter is server-applied; the UI never fakes an unsupported filter client-side.
public sealed record GetProductListQuery : IRequest<Response<ProductListResultDto>>
{
    /// <summary>Free-text over ProductCode + ProductName.</summary>
    public string? Search { get; init; }

    public string? ProductStatus { get; init; }
    public Guid? BrandId { get; init; }
    public string? ProductType { get; init; }
    public string? DosageForm { get; init; }
    public Guid? TherapeuticAreaId { get; init; }
    public bool IncludeArchived { get; init; }
}
