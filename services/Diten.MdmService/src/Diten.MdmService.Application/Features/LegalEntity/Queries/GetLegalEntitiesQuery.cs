using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Queries;

// MOD-0288 lookup feed (no filters → full list, OrgUnit picker) + MOD-0220 FULL list filters.
// All filters are optional and applied server-side; an empty query returns every tenant entity (back-compat).
public sealed record GetLegalEntitiesQuery : IRequest<Response<IReadOnlyList<LegalEntityDetailDto>>>
{
    public string? Country { get; init; }
    public string? OperationalStatus { get; init; }
    public string? EvidenceStatus { get; init; }
    public string? BaseCurrency { get; init; }
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public bool IncompleteOnly { get; init; }
}
