using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.QueryHandlers;

/// <summary>GET /api/mdm/banks — the tenant's banks. Validation has already refused a search over 100 characters (AC-59).</summary>
public sealed class GetBankListHandler : IRequestHandler<GetBankListQuery, Response<BankListResultDto>>
{
    private readonly IBankRepository _banks;

    public GetBankListHandler(IBankRepository banks) => _banks = banks;

    public async Task<Response<BankListResultDto>> Handle(GetBankListQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        BankListSort.TryParse(request.SortBy, out var order);
        BankListSort.TryParseDirection(request.SortDir, out var descending);
        var criteria = new BankListCriteria(
            BankRules.NormalizeText(request.Search),
            BankRules.TryParseStatus(request.Status, out var status) ? status : null,
            BankRules.NormalizeCountry(request.CountryCode),
            order,
            descending,
            request.Page,
            request.PageSize);
        var page = await _banks.ListAsync(criteria, cancellationToken);
        return Response<BankListResultDto>.Success(new BankListResultDto(
            page.Items.Select(BankMappings.ToListItem).ToArray(), page.Total, request.Page, request.PageSize));
    }
}
