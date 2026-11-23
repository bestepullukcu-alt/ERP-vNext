using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

/// <summary><c>ExpectedVersion</c> is the request's <c>If-Match</c>: a stale one is 409 MDM_BANK_VERSION_CONFLICT.</summary>
public sealed record UpdateBankCommand(Guid BankId, BankWriteRequest Request, int ExpectedVersion, Guid Actor)
    : IRequest<Response<BankDetailDto>>;
