using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.QueryHandlers;

/// <summary>GET /api/mdm/bank-branches/{id} — a branch of another tenant, a deleted one and a missing one are the same 404.</summary>
public sealed class GetBankBranchByIdHandler : IRequestHandler<GetBankBranchByIdQuery, Response<BankBranchDto>>
{
    private readonly IBankBranchRepository _branches;

    public GetBankBranchByIdHandler(IBankBranchRepository branches) => _branches = branches;

    public async Task<Response<BankBranchDto>> Handle(GetBankBranchByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var branch = await _branches.GetByIdAsync(request.BranchId, cancellationToken);
        return branch is null
            ? BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.BranchNotFound, 404)
            : Response<BankBranchDto>.Success(BankMappings.ToDto(branch));
    }
}
