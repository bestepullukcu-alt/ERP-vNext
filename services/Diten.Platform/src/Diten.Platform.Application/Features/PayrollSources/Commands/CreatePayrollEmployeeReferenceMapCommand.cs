using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record CreatePayrollEmployeeReferenceMapCommand(Guid SourceProfileId, PayrollEmployeeReferenceMapRequest Request) : IRequest<Response<Guid>>;
