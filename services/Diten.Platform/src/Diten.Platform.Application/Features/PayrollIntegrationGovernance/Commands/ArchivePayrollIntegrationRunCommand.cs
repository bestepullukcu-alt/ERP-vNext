using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record ArchivePayrollIntegrationRunCommand(Guid RunId) : IRequest<Response<NoContent>>;
