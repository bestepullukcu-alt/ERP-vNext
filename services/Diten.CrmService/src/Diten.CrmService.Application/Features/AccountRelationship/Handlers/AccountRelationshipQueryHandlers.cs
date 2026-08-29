using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountRelationship.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.AccountRelationship.Handlers;

public sealed class ListRelationshipsForAccountHandler
    : IRequestHandler<ListRelationshipsForAccountQuery, Response<IReadOnlyList<RelatedAccountDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IReferenceMetadataReader _metadataReader;

    public ListRelationshipsForAccountHandler(
        ITenantContext tenant, IAccountRepository accounts, IAccountRelationshipRepository relationships, IReferenceMetadataReader metadataReader)
    {
        _tenant = tenant;
        _accounts = accounts;
        _relationships = relationships;
        _metadataReader = metadataReader;
    }

    public async Task<Response<IReadOnlyList<RelatedAccountDto>>> Handle(ListRelationshipsForAccountQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<RelatedAccountDto>>.Fail("Tenant context is required.", 400);
        }

        if (await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken) is null)
        {
            return Response<IReadOnlyList<RelatedAccountDto>>.Fail("Account not found.", 404);
        }

        var relationships = await _relationships.ListByAccountAsync(tenantId, request.AccountId, cancellationToken);
        var metadataCache = new Dictionary<string, RelationshipTypeMetadata>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<RelatedAccountDto>();

        foreach (var rel in relationships)
        {
            var queriedIsSource = rel.SourceAccountId == request.AccountId;
            var relatedId = queriedIsSource ? rel.TargetAccountId : rel.SourceAccountId;
            var related = await _accounts.GetByIdAsync(tenantId, relatedId, cancellationToken);
            if (related is null)
            {
                continue; // related account soft-deleted — skip, never fabricate.
            }

            if (!metadataCache.TryGetValue(rel.RelationshipType, out var metadata))
            {
                var attrs = await _metadataReader.GetValueAttributesAsync(RelationshipReferenceValidation.TypeSet, rel.RelationshipType, cancellationToken);
                metadata = RelationshipTypeMetadata.Parse(attrs);
                metadataCache[rel.RelationshipType] = metadata;
            }

            rows.Add(AccountRelationshipMapper.ToRelatedAccount(rel, related, metadata, queriedIsSource));
        }

        return Response<IReadOnlyList<RelatedAccountDto>>.Success(rows);
    }
}

public sealed class GetAccountRelationshipByIdHandler : IRequestHandler<GetAccountRelationshipByIdQuery, Response<AccountRelationshipDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRelationshipRepository _relationships;

    public GetAccountRelationshipByIdHandler(ITenantContext tenant, IAccountRelationshipRepository relationships)
    {
        _tenant = tenant;
        _relationships = relationships;
    }

    public async Task<Response<AccountRelationshipDto>> Handle(GetAccountRelationshipByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountRelationshipDto>.Fail("Tenant context is required.", 400);
        }

        var relationship = await _relationships.GetByIdAsync(tenantId, request.RelationshipId, cancellationToken);
        if (relationship is null || relationship.SourceAccountId != request.SourceAccountId)
        {
            return Response<AccountRelationshipDto>.Fail("Account relationship not found.", 404);
        }

        return Response<AccountRelationshipDto>.Success(AccountRelationshipMapper.ToDto(relationship));
    }
}
