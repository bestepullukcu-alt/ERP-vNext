using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record RecordPayrollResultReferenceCommand(Guid SourceProfileId, PayrollResultReferenceRequest Request) : IRequest<Response<Guid>>;
