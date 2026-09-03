using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record GetInitiativeLifecycleContractsV2Query
    : IRequest<Response<InitiativeLifecycleContractsV2>>;
