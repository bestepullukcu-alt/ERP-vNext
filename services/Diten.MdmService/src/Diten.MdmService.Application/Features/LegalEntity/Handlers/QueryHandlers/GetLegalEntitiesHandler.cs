using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;

public sealed class GetLegalEntitiesHandler : IRequestHandler<Queries.GetLegalEntitiesQuery, Response<IReadOnlyList<LegalEntityDetailDto>>>
{
    private readonly ILegalEntityRepository _repository;

    public GetLegalEntitiesHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<LegalEntityDetailDto>>> Handle(Queries.GetLegalEntitiesQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        IEnumerable<Domain.Entities.LegalEntity> filtered = entities;

        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            filtered = filtered.Where(e => string.Equals(e.CountryCode, request.Country.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.OperationalStatus))
        {
            filtered = filtered.Where(e => string.Equals(e.OperationalStatus.ToString(), request.OperationalStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.EvidenceStatus))
        {
            filtered = filtered.Where(e => string.Equals(e.EvidenceStatus.ToString(), request.EvidenceStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.BaseCurrency))
        {
            filtered = filtered.Where(e => string.Equals(e.BaseCurrencyCode, request.BaseCurrency.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (request.CreatedFrom is { } from)
        {
            filtered = filtered.Where(e => e.CreatedAt >= from);
        }

        if (request.CreatedTo is { } to)
        {
            filtered = filtered.Where(e => e.CreatedAt <= to);
        }

        if (request.IncompleteOnly)
        {
            // Incomplete = required fields missing (CompletenessScore < 100, or never computed on legacy rows).
            filtered = filtered.Where(e => (e.CompletenessScore ?? 0) < 100);
        }

        IReadOnlyList<LegalEntityDetailDto> items = filtered
            .Select(LegalEntityMappings.ToDetailDto)
            .ToList();
        return Response<IReadOnlyList<LegalEntityDetailDto>>.Success(items);
    }
}
