using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.QueryHandlers;

/// <summary>GET /api/mdm/banks/{bankId}/branches — the branches of a bank of the tenant (404 when the bank is not the tenant's).</summary>
public sealed class GetBankBranchListHandler : IRequestHandler<GetBankBranchListQuery, Response<BankBranchListResultDto>>
{
    private readonly IBankRepository _banks;
    private readonly IBankBranchRepository _branches;

    public GetBankBranchListHandler(IBankRepository banks, IBankBranchRepository branches)
    {
        _banks = banks;
        _branches = branches;
    }

    public async Task<Response<BankBranchListResultDto>> Handle(
        GetBankBranchListQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await _banks.GetByIdAsync(request.BankId, cancellationToken) is null)
            return BankHandlerSupport.Fail<BankBranchListResultDto>(BankErrorCodes.NotFound, 404);

        var criteria = new BankBranchListCriteria(
            request.BankId,
            BankRules.NormalizeText(request.Search),
            BankRules.TryParseStatus(request.Status, out var status) ? status : null,
            request.Page,
            request.PageSize);
        var page = await _branches.ListAsync(criteria, cancellationToken);
        return Response<BankBranchListResultDto>.Success(new BankBranchListResultDto(
            page.Items.Select(BankMappings.ToDto).ToArray(), page.Total, request.Page, request.PageSize));
    }
}
