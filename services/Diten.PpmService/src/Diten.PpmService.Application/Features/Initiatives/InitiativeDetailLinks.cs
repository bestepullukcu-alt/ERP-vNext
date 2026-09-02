using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeDetailLinks(IReadOnlyList<InitiativeTypedReference> References);
