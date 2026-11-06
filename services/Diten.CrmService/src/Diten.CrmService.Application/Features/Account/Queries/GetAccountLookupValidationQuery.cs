using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Queries;

/// <summary>MOD-0149 §30 U-1 <c>GET /api/crm/accounts/{accountId}/lookup-validation</c> (CRM-ACCOUNT-LOOKUP 1.0 §2).</summary>
public sealed record GetAccountLookupValidationQuery(Guid AccountId) : IRequest<Response<AccountLookupValidationDto>>;
