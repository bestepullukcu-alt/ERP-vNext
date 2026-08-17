using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Commands;

public sealed record DeprecateInterfaceRequest(string InterfaceCode, string Version, string Reason)
    : IRequest<Response<InterfaceActiveSnapshotDto>>;
