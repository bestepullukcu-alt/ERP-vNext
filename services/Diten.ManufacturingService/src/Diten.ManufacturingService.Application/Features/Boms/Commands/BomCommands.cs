using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Commands;

public sealed record CreateBomDraftCommand(CreateBomDraftRequest Body) : IRequest<Response<BomView>>;

public sealed record UpdateBomDraftCommand(Guid BomVersionId, UpdateBomDraftRequest Body) : IRequest<Response<BomView>>;

/// <summary>Draft → Effective, and the previous Effective → Superseded in the same transaction: a lifecycle transition.</summary>
public sealed record ReleaseBomVersionCommand(Guid BomVersionId, ReleaseBomVersionRequest Body) : IRequest<Response<BomView>>;

public sealed record DeleteBomDraftCommand(Guid BomVersionId, int RowVersion) : IRequest<Response<NoContent>>;
