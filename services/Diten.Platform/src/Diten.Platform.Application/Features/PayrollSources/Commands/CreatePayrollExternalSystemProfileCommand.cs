using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record CreatePayrollExternalSystemProfileCommand(PayrollExternalSystemProfileRequest Request) : IRequest<Response<Guid>>;
