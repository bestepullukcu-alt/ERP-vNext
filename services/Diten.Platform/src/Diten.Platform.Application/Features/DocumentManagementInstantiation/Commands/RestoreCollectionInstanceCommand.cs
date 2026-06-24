using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;

// MOD-0028-FU05 follow-up: restore (un-archive) a previously archived company CollectionInstance node.
// Restores the node + its descendant subtree and re-activates required ancestors so no active node is
// orphaned under an archived parent.
public sealed record RestoreCollectionInstanceCommand(
    Guid Id,
    string CorrelationId)
    : IRequest<Response<CollectionInstanceStatusChangeResultModel>>;
