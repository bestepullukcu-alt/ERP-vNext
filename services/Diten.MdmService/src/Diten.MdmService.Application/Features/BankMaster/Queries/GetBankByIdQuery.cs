using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Queries;

public sealed record GetBankByIdQuery(Guid BankId) : IRequest<Response<BankDetailDto>>;
