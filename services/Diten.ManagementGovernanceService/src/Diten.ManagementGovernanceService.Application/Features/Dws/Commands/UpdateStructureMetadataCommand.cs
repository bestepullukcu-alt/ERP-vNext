using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record UpdateStructureMetadataCommand(UpdateStructureMetadataRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<UpdateStructureMetadataResult>>;
