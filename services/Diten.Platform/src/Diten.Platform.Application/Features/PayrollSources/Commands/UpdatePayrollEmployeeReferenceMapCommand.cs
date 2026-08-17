using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record UpdatePayrollEmployeeReferenceMapCommand(Guid SourceProfileId, Guid MapId, PayrollEmployeeReferenceMapRequest Request) : IRequest<Response<NoContent>>;
