using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Commands;

public sealed record CreateProfessionalReputationLedgerReadinessCommand(ProfessionalReputationLedgerReadinessCreateRequest Request) : IRequest<Response<Guid>>;
