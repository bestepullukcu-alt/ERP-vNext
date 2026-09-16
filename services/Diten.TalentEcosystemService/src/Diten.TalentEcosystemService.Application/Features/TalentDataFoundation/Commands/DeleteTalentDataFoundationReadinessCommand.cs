using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Commands;

public sealed record DeleteTalentDataFoundationReadinessCommand(Guid Id) : IRequest<Response<bool>>;
