using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Queries;

/// <summary>MOD-0149 §30 U-3 <c>GET /api/crm/accounts/lookup</c> (CRM-ACCOUNT-LOOKUP 1.0 §2a). A null page, pageSize or
/// billableOnly takes the contract default (1, 20, false).</summary>
public sealed record SearchAccountLookupQuery(string? Search, int? Page, int? PageSize, bool? BillableOnly)
    : IRequest<Response<AccountLookupPageDto>>;
