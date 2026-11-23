using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Queries;

/// <summary>
/// The bank register. <c>Search</c> is plain text (trimmed, at most 100 characters; matched case-insensitively against the
/// name, code and BIC — never as a pattern, AC-59). <c>TenantId</c> exists so a request that sends one is refused (400).
/// </summary>
public sealed record GetBankListQuery : IRequest<Response<BankListResultDto>>
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? CountryCode { get; init; }
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? TenantId { get; init; }
}
