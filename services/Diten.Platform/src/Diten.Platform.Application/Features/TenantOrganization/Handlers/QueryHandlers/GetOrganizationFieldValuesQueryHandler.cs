using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

/// <summary>
/// ⚠ EVERY POLICY DECISION HAPPENS HERE, BEFORE MONGO IS TOUCHED. The repository receives a spec it can
/// execute literally; it never decides whether a definition may be filtered, whether an operator is legal for
/// a type, or how big a page may be. Splitting that judgement across two layers is how one of them ends up
/// with a rule the other assumed it had.
/// </summary>
public sealed class GetOrganizationFieldValuesQueryHandler
    : IRequestHandler<GetOrganizationFieldValuesQuery, Response<OrganizationFieldValuePageDto>>
{
    private readonly IOrganizationFieldDefinitionRepository _definitions;
    private readonly IOrganizationFieldValueRepository _values;
    private readonly IActorPermissionContext _permissions;

    public GetOrganizationFieldValuesQueryHandler(
        IOrganizationFieldDefinitionRepository definitions,
        IOrganizationFieldValueRepository values,
        IActorPermissionContext permissions)
    {
        _definitions = definitions;
        _values = values;
        _permissions = permissions;
    }

    public async Task<Response<OrganizationFieldValuePageDto>> Handle(
        GetOrganizationFieldValuesQuery request,
        CancellationToken ct)
    {
        if (OrganizationFieldDefinitionRules.ValidatePaging(request.Page, request.PageSize) is { } pagingError)
        {
            return Fail(pagingError);
        }

        var all = await _definitions.GetAllAsync(ct);
        var byId = all.ToDictionary(d => d.Id);

        var filters = request.Filters ?? [];
        var namedDefinitions = filters.Select(f => f.DefinitionId).Distinct().ToList();
        var specs = new List<OrganizationFieldValueFilterSpec>(filters.Count);

        foreach (var filter in filters)
        {
            if (!byId.TryGetValue(filter.DefinitionId, out var definition) || definition.DeletedAt is not null)
            {
                return Fail(("Organization Unit field definition not found.", 404));
            }

            if (!OrganizationFieldDefinitionRules.TryParseOperator(filter.Operator, out var op))
            {
                return Fail(($"'{filter.Operator}' is not a supported filter operator.", 400));
            }

            var values = filter.Values ?? [];
            var error = OrganizationFieldDefinitionRules.ValidateFilter(
                definition, op, values, isTheOnlyDefinitionInTheQuery: namedDefinitions.Count == 1);
            if (error is { } filterError)
            {
                return Fail(filterError);
            }

            var canonical = new List<string>(values.Count);
            foreach (var raw in values)
            {
                /*
                 * ⚠ FILTER VALUES ARE CANONICALIZED THE SAME WAY STORED ONES ARE. Otherwise "01" would never
                 * match the stored "1" and the query would answer "no such unit" about a unit that has it.
                 * StartsWith is the one exception: a prefix is not a complete value, so it is matched as text.
                 */
                if (op == OrganizationFieldFilterOperator.StartsWith)
                {
                    canonical.Add(raw?.Trim() ?? string.Empty);
                    continue;
                }

                var (value, message, status) =
                    OrganizationFieldDefinitionRules.CanonicalizeValue(definition, raw);
                if (message is not null)
                {
                    return Fail((message, status));
                }

                if (value is not null)
                {
                    canonical.Add(value);
                }
            }

            if (canonical.Count == 0)
            {
                return Fail(("A filter needs at least one value.", 400));
            }

            specs.Add(new OrganizationFieldValueFilterSpec(definition.Id, op, canonical));
        }

        if (request.SortDefinitionId is { } sortId)
        {
            if (!byId.TryGetValue(sortId, out var sortDefinition) || sortDefinition.DeletedAt is not null)
            {
                return Fail(("Organization Unit field definition not found.", 404));
            }

            if (OrganizationFieldDefinitionRules.ValidateSort(sortDefinition) is { } sortError)
            {
                return Fail(sortError);
            }
        }

        var spec = new OrganizationFieldValueQuerySpec(
            request.OrganizationUnitId,
            specs,
            request.SortDefinitionId,
            request.SortDescending,
            Skip: (request.Page - 1) * request.PageSize,
            Take: request.PageSize);

        var rows = await _values.QueryAsync(spec, ct);

        /*
         * ⚠ CLASSIFICATION IS ENFORCED HERE, ON THE WAY OUT (§14). Storing a classification and not applying
         * it on read is decoration, not control — and the governance fields this mechanism exists for are
         * precisely the sensitive ones. A caller without the grant still sees that the field is set; it does
         * not see what it says.
         */
        IReadOnlyList<OrganizationFieldValueDto> items = rows
            .Select(v =>
            {
                var known = byId.TryGetValue(v.DefinitionId, out var d);
                var mayRead = _permissions.Has(OrganizationFieldMapper.ReadPermissionFor(v.Classification));
                return OrganizationFieldMapper.ToDto(v, known ? d!.Code : string.Empty, mayRead);
            })
            .ToList();

        return Response<OrganizationFieldValuePageDto>.Success(
            new OrganizationFieldValuePageDto(items, request.Page, request.PageSize));
    }

    private static Response<OrganizationFieldValuePageDto> Fail((string Message, int StatusCode) error)
        => Response<OrganizationFieldValuePageDto>.Fail(error.Message, error.StatusCode);
}
