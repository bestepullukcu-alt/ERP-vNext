using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Commands;

public sealed record ImportInterfaceManifestRequest(InterfaceManifestDocument Manifest) : IRequest<Response<InterfaceManifestImportResultDto>>;
