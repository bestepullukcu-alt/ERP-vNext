using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// MOD-0290-FU04 S5-FIX1 (CT K2) — the "Stock behaviour" section's O-8 locks, field by field: on an approved record every
/// FILLED scalar of the level is locked; on a Draft none is; an empty one never is. The sabotage target "the FG GTIN is
/// dropped from the locks".
/// </summary>
public sealed class StockBehaviourViewTests
{
    public static TheoryData<string, string> EveryLevelAndScalar() => new()
    {
        { "gsku", StockBehaviourView.BaseUomCode }, { "gsku", StockBehaviourView.ShelfLifeDays },
        { "gsku", StockBehaviourView.RetestDays }, { "gsku", StockBehaviourView.MinRemainingShelfLifeDays },
        { "gsku", StockBehaviourView.LotControlled }, { "gsku", StockBehaviourView.Stockable },
        { "lsku", StockBehaviourView.SerialControlled },
        { "fg", StockBehaviourView.ShelfLifeDays }, { "fg", StockBehaviourView.LotControlled },
        { "fg", StockBehaviourView.SerialControlled }, { "fg", StockBehaviourView.Stockable }, { "fg", StockBehaviourView.Gtin }
    };

    [Theory]
    [MemberData(nameof(EveryLevelAndScalar))]
    public void S5_FIX1_A_filled_scalar_is_locked_on_an_approved_record_and_free_on_a_draft(string level, string field)
    {
        Assert.Contains(field, View(level, field, ProductIdentityLifecycleStatus.IdentityApproved, filled: true).LockedFields);
        Assert.DoesNotContain(field, View(level, field, ProductIdentityLifecycleStatus.Draft, filled: true).LockedFields);
        Assert.DoesNotContain(field, View(level, field, ProductIdentityLifecycleStatus.IdentityApproved, filled: false).LockedFields);
    }

    private static StockBehaviourDto View(string level, string field, ProductIdentityLifecycleStatus status, bool filled)
    {
        var gsku = new Gsku { PackQuantity = 30m, PackUomCode = "C62", LifecycleStatus = status };
        var lsku = new Lsku { LifecycleStatus = status };
        var fg = new FinishedGood { LifecycleStatus = status };
        if (filled)
        {
            switch (level, field)
            {
                case ("gsku", StockBehaviourView.BaseUomCode): gsku.BaseUomCode = "C62"; break;
                case ("gsku", StockBehaviourView.ShelfLifeDays): gsku.ShelfLifeDays = 365; break;
                case ("gsku", StockBehaviourView.RetestDays): gsku.RetestDays = 30; break;
                case ("gsku", StockBehaviourView.MinRemainingShelfLifeDays): gsku.MinRemainingShelfLifeDays = 30; break;
                case ("gsku", StockBehaviourView.LotControlled): gsku.LotControlled = true; break;
                case ("gsku", StockBehaviourView.Stockable): gsku.Stockable = false; break;
                case ("lsku", _): lsku.SerialControlled = true; break;
                case ("fg", StockBehaviourView.ShelfLifeDays): fg.ShelfLifeDays = 365; break;
                case ("fg", StockBehaviourView.LotControlled): fg.LotControlled = true; break;
                case ("fg", StockBehaviourView.SerialControlled): fg.SerialControlled = true; break;
                case ("fg", StockBehaviourView.Stockable): fg.Stockable = false; break;
                case ("fg", StockBehaviourView.Gtin): fg.Gtin = "08690000000017"; break;
            }
        }
        return level switch
        {
            "gsku" => StockBehaviourView.ForGsku(gsku),
            "lsku" => StockBehaviourView.ForLsku(gsku, lsku),
            _ => StockBehaviourView.ForFinishedGood(gsku, fg)
        };
    }
}
