using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Queries;

/// <summary>The branches of one bank. <c>TenantId</c> exists so a request that sends one is refused (400).</summary>
public sealed record GetBankBranchListQuery : IRequest<Response<BankBranchListResultDto>>
{
    public Guid BankId { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? TenantId { get; init; }
}
