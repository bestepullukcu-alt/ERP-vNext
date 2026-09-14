using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Commands;

public sealed record CreateTalentDataFoundationReadinessCommand(TalentDataFoundationReadinessCreateRequest Request) : IRequest<Response<Guid>>;
