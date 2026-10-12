using Diten.ManufacturingService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Commands;

// Every write command declares its central-audit record (BL-501 pre-wiring, see ICentralAuditDeclaration). The BOM
// version history (path c) is written by the handlers as before; this adds nothing at runtime until BL-501 is wired.

public sealed record CreateBomDraftCommand(CreateBomDraftRequest Body) : IRequest<Response<BomView>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CentralAuditMetadata.Bom("Create");

    public Guid? CentralAuditEntityId(object? response) =>
        Guid.TryParse((response as Response<BomView>)?.Data?.BomVersionId, out var id) ? id : null;
}

public sealed record UpdateBomDraftCommand(Guid BomVersionId, UpdateBomDraftRequest Body) : IRequest<Response<BomView>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CentralAuditMetadata.Bom("Update");

    public Guid? CentralAuditEntityId(object? response) => BomVersionId;
}

/// <summary>Draft → Effective, and the previous Effective → Superseded in the same transaction: a lifecycle transition.</summary>
public sealed record ReleaseBomVersionCommand(Guid BomVersionId, ReleaseBomVersionRequest Body) : IRequest<Response<BomView>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CentralAuditMetadata.Bom("LifecycleTransition");

    public Guid? CentralAuditEntityId(object? response) => BomVersionId;
}

public sealed record DeleteBomDraftCommand(Guid BomVersionId, int RowVersion) : IRequest<Response<NoContent>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CentralAuditMetadata.Bom("Delete");

    public Guid? CentralAuditEntityId(object? response) => BomVersionId;
}
