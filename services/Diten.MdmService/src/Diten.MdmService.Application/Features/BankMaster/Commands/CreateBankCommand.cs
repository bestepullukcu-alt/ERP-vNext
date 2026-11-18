using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

/// <summary>Creates a Draft bank. <c>Actor</c> is the token's <c>sub</c> (audit identity only, never authorization).</summary>
public sealed record CreateBankCommand(BankWriteRequest Request, Guid Actor) : IRequest<Response<BankDetailDto>>;
