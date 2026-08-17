using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Commands;

public sealed record RecordHrisSyncCheckpointCommand(Guid SourceProfileId, HrisSyncCheckpointRequest Request) : IRequest<Response<Guid>>;
