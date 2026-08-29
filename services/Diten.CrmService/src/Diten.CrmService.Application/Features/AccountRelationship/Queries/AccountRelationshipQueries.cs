using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.AccountRelationship.Queries;

/// <summary>Account 360 "Related Accounts" (serves GET /accounts/{id}/relationships and /accounts/{id}/related-accounts).</summary>
public sealed record ListRelationshipsForAccountQuery(Guid AccountId)
    : IRequest<Response<IReadOnlyList<RelatedAccountDto>>>;

public sealed record GetAccountRelationshipByIdQuery(Guid SourceAccountId, Guid RelationshipId)
    : IRequest<Response<AccountRelationshipDto>>;
