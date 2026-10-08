using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountRelationship.Handlers;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.AccountRelationship.Queries;

/// <summary>
/// WP-VP-3D (D5) — <c>GET api/crm/accounts/related?accountIds=a,b,c&amp;relationType=pharmacy</c>: the bulk twin of
/// <c>/accounts/{id}/related-accounts</c> (<see cref="ListRelationshipsForAccountQuery"/>) — the same
/// <see cref="RelatedAccountDto"/> rows (same direction / inverse-label rule, <see cref="AccountRelationshipMapper"/>),
/// grouped per requested account. A READ query.
/// <para><b>Bounds.</b> At most <see cref="MaxIds"/> ids — more is 400 <c>too_many_ids</c>; none is 400
/// <c>account_ids_required</c>; a malformed id is 400 <c>invalid_account_ids</c>. An id that does not exist in the
/// tenant gets no group (nothing is invented).</para>
/// <para><b>relationType</b> (optional, case-insensitive) keeps a row when it equals the relationship type OR the related
/// account's type — so <c>relationType=pharmacy</c> answers "the linked pharmacies" whatever the relationship code.</para>
/// <para>Cost, whatever the id count: relationships (1 read, <c>$in</c> on both sides) + accounts by id (1 read) + one
/// reference-metadata read per DISTINCT relationship type.</para>
/// </summary>
public sealed record ListRelatedAccountsBulkQuery(string? AccountIds, string? RelationType = null)
    : IRequest<Response<RelatedAccountsBulkDto>>;

/// <summary>Per requested account its related accounts (requested order kept).</summary>
public sealed record RelatedAccountsBulkDto(IReadOnlyList<RelatedAccountsGroupDto> Groups);

public sealed record RelatedAccountsGroupDto(Guid AccountId, IReadOnlyList<RelatedAccountDto> Items);

public static class RelatedAccountsBulkCodes
{
    public const string TooManyIds = "too_many_ids";
    public const string AccountIdsRequired = "account_ids_required";
    public const string InvalidAccountIds = "invalid_account_ids";
}

public sealed class ListRelatedAccountsBulkQueryHandler
    : IRequestHandler<ListRelatedAccountsBulkQuery, Response<RelatedAccountsBulkDto>>
{
    public const int MaxIds = 100;

    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IReferenceMetadataReader _metadataReader;

    public ListRelatedAccountsBulkQueryHandler(
        ITenantContext tenant,
        IAccountRepository accounts,
        IAccountRelationshipRepository relationships,
        IReferenceMetadataReader metadataReader)
    {
        _tenant = tenant;
        _accounts = accounts;
        _relationships = relationships;
        _metadataReader = metadataReader;
    }

    public async Task<Response<RelatedAccountsBulkDto>> Handle(
        ListRelatedAccountsBulkQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<RelatedAccountsBulkDto>.Fail("Tenant context is required.", 400);
        }

        var raw = (request.AccountIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (raw.Length == 0)
        {
            return Response<RelatedAccountsBulkDto>.Fail(
                new[] { RelatedAccountsBulkCodes.AccountIdsRequired, "accountIds is required." }, 400);
        }

        var ids = new List<Guid>();
        foreach (var token in raw)
        {
            if (!Guid.TryParse(token, out var id) || id == Guid.Empty)
            {
                return Response<RelatedAccountsBulkDto>.Fail(
                    new[] { RelatedAccountsBulkCodes.InvalidAccountIds, "accountIds must be a comma-separated list of ids." }, 400);
            }

            if (!ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        if (ids.Count > MaxIds)
        {
            return Response<RelatedAccountsBulkDto>.Fail(
                new[] { RelatedAccountsBulkCodes.TooManyIds, $"At most {MaxIds} account ids per request." }, 400);
        }

        var relationships = (await _relationships.ListByAccountIdsAsync(tenantId, ids, cancellationToken))
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .ToList();

        var accountIds = ids
            .Concat(relationships.SelectMany(r => new[] { r.SourceAccountId, r.TargetAccountId }))
            .Distinct()
            .ToList();
        var accounts = (await _accounts.ListByIdsAsync(tenantId, accountIds, cancellationToken))
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .GroupBy(a => a.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var filter = string.IsNullOrWhiteSpace(request.RelationType) ? null : request.RelationType.Trim();
        var metadataCache = new Dictionary<string, RelationshipTypeMetadata>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<RelatedAccountsGroupDto>();
        foreach (var accountId in ids.Where(accounts.ContainsKey))
        {
            var rows = new List<RelatedAccountDto>();
            foreach (var rel in relationships.Where(r => r.SourceAccountId == accountId || r.TargetAccountId == accountId))
            {
                var queriedIsSource = rel.SourceAccountId == accountId;
                var relatedId = queriedIsSource ? rel.TargetAccountId : rel.SourceAccountId;
                if (!accounts.TryGetValue(relatedId, out var related))
                {
                    continue; // related account soft-deleted — skip, never fabricate (the single endpoint's rule).
                }

                if (filter is not null
                    && !string.Equals(rel.RelationshipType, filter, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(related.AccountType, filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!metadataCache.TryGetValue(rel.RelationshipType, out var metadata))
                {
                    var attrs = await _metadataReader.GetValueAttributesAsync(
                        RelationshipReferenceValidation.TypeSet, rel.RelationshipType, cancellationToken);
                    metadata = RelationshipTypeMetadata.Parse(attrs);
                    metadataCache[rel.RelationshipType] = metadata;
                }

                rows.Add(AccountRelationshipMapper.ToRelatedAccount(rel, related, metadata, queriedIsSource));
            }

            groups.Add(new RelatedAccountsGroupDto(accountId, rows));
        }

        return Response<RelatedAccountsBulkDto>.Success(new RelatedAccountsBulkDto(groups));
    }
}
