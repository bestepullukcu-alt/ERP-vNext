using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Queries;

/// <summary>MOD-0149 §32 U-4 <c>GET /api/crm/accounts/{accountId}/ship-to</c> (CRM-ACCOUNT-LOOKUP 1.1 §2).</summary>
public sealed record GetAccountShipToQuery(Guid AccountId) : IRequest<Response<AccountShipToDto>>;
