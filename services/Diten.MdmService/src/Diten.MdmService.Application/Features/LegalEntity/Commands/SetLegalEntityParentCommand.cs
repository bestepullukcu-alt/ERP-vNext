using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

/// <summary>
/// Re-parents a legal entity (or makes it a root when ParentId is null) within the same tenant.
/// Guards: cannot parent to itself, cannot parent to a different tenant, cannot create a cycle.
/// </summary>
public sealed record SetLegalEntityParentCommand(
    Guid LegalEntityId,
    Guid? ParentId) : IRequest<Response<NoContent>>;
