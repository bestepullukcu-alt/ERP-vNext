using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;

// MOD-0028-FU05 follow-up (company-local override): soft-archive a company CollectionInstance node and,
// since a container cannot be left with orphaned active children, its whole descendant subtree.
public sealed record ArchiveCollectionInstanceCommand(
    Guid Id,
    string CorrelationId)
    : IRequest<Response<CollectionInstanceStatusChangeResultModel>>;
