using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record ArchivePayrollExternalSystemProfileCommand(Guid Id) : IRequest<Response<NoContent>>;
