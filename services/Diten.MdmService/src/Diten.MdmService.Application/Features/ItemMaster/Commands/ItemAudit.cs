using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// MOD-0290-FU04 §21 (AUD-001 path b, `mdm-merkezi-iletim`): every item write command is marked from day one; a refused
// one is forwarded too (Outcome = Failed). The item is tenant-wide master data (Decision 6): these commands are not
// legal-entity scope mutations and live outside the product scope inventory (CT decision 5).
public static class ItemAudit
{
    public const string EntityType = "Item";
    public const string SourceModule = "product-item-sku-master";

    public static AuditMetadata Metadata(AuditOperation operation) => new(
        Category: AuditCategory.MasterData, Operation: operation, EntityType: EntityType, SourceModule: SourceModule);
}
