using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record UpdatePayrollContractProfileCommand(Guid SourceProfileId, PayrollContractProfileRequest Request) : IRequest<Response<NoContent>>;
