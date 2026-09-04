using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record GetInitiativeContractsV2Query : IRequest<Response<InitiativeContractsV2>>;
