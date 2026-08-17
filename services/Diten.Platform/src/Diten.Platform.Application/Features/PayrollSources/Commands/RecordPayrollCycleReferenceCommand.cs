using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record RecordPayrollCycleReferenceCommand(Guid SourceProfileId, PayrollCycleReferenceRequest Request) : IRequest<Response<Guid>>;
