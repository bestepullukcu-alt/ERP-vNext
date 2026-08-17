using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;

public sealed record ArchiveConsentVisibilityPolicyCommand(Guid Id) : IRequest<Response<NoContent>>;
