using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Queries;

public sealed record GetInterfaceDefinitionSnapshotRequest(string InterfaceCode, string Version)
    : IRequest<Response<InterfaceActiveSnapshotDto>>;
