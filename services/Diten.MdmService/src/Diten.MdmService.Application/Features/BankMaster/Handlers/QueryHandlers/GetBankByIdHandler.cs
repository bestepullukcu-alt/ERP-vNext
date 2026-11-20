using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.QueryHandlers;

/// <summary>GET /api/mdm/banks/{id}. A bank of another tenant, a deleted one and a missing one are the same 404.</summary>
public sealed class GetBankByIdHandler : IRequestHandler<GetBankByIdQuery, Response<BankDetailDto>>
{
    private readonly IBankRepository _banks;

    public GetBankByIdHandler(IBankRepository banks) => _banks = banks;

    public async Task<Response<BankDetailDto>> Handle(GetBankByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bank = await _banks.GetByIdAsync(request.BankId, cancellationToken);
        return bank is null
            ? BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.NotFound, 404)
            : Response<BankDetailDto>.Success(BankMappings.ToDetail(bank));
    }
}
