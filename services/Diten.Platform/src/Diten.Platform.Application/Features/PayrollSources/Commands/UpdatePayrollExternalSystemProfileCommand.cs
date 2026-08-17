using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record UpdatePayrollExternalSystemProfileCommand(Guid Id, PayrollExternalSystemProfileRequest Request) : IRequest<Response<NoContent>>;
