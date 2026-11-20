using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Queries;

public sealed record GetBankBranchByIdQuery(Guid BranchId) : IRequest<Response<BankBranchDto>>;
