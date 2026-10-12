using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// MOD-0174 P3 — the Stock Ledger form's lot field (pack §11.4) rendered by the REAL FU01 partial: the lot is a PICKER of
/// registered lots (inbound only — the matrix block), "+ Yeni lot oluştur" and its modal exist only for a holder of
/// <c>trace.lots.create</c> (CT-D5a, an in-form control), the K-F retest confirmation, the three serial methods and the
/// GTIN-null warning surface, the lot vocabulary payload; the typed lot (W-UI-02) stays reachable with its badge until P4.
/// </summary>
public sealed class StockLedgerLotFieldTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Create = "/Views/Inventory/StockMovements/Create.cshtml";
    private const string Entry = "/Views/Inventory/StockLedger/Create.cshtml";

    private readonly WebApplicationFactory<Program> _factory;

    public StockLedgerLotFieldTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData(Create)]
    [InlineData(Entry)]
    public async Task The_lot_field_is_a_registered_lot_picker_with_the_serial_methods(string view)
    {
        var model = view == Entry ? StockMovementFormViewModel.ForEntry(new QueryCollection()) : StockMovementFormViewModel.ForCreate(new QueryCollection());
        var html = await TraceViewRenderer.RenderAsync(_factory, view, model, "inventory.stock.read", "inventory.movements.create");

        Assert.Contains("<select id=\"lotId\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("lotIdKnown", html, StringComparison.Ordinal); // the W-UI-02 datalist is gone
        Assert.Contains("data-matrix=\"lot\"", html, StringComparison.Ordinal); // inbound-only block (pack §12 matrix)
        Assert.Contains("id=\"lotRetestConfirm\"", html, StringComparison.Ordinal); // K-F
        Assert.Contains("id=\"lotIdTyped\"", html, StringComparison.Ordinal);       // W-UI-02 until P4, with its badge
        foreach (var method in new[] { "single", "range", "scan" })
            Assert.Contains($"data-serial-method=\"{method}\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"serialGtinWarning\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"lotfield-l10n\"", html, StringComparison.Ordinal);
        Assert.Contains("\"Lot.BlockedDisabled\":", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CT_D5a_the_quick_lot_exists_only_for_a_holder_of_trace_lots_create()
    {
        var model = StockMovementFormViewModel.ForCreate(new QueryCollection());
        var without = await TraceViewRenderer.RenderAsync(_factory, Create, model, "inventory.stock.read", "inventory.movements.create");
        Assert.DoesNotContain("btnQuickLot", without, StringComparison.Ordinal);
        Assert.DoesNotContain("modalQuickLot", without, StringComparison.Ordinal);

        var with = await TraceViewRenderer.RenderAsync(_factory, Create, model, "inventory.stock.read", "inventory.movements.create", "trace.lots.create");
        Assert.Contains("id=\"btnQuickLot\"", with, StringComparison.Ordinal);
        Assert.Contains("id=\"modalQuickLot\"", with, StringComparison.Ordinal);
        // The modal is outside the movement form (no nested form) and posts nothing of its own besides /Lots/create.
        Assert.True(with.IndexOf("id=\"modalQuickLot\"", StringComparison.Ordinal) > with.IndexOf("</form>", StringComparison.Ordinal));
        Assert.Contains("quick-create.js", with, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_registered_lot_on_a_corrected_row_carries_no_unregistered_badge()
    {
        var row = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["id"] = Guid.NewGuid().ToString(), ["no"] = "INV-2026-0000007", ["type"] = "OPENING_BALANCE",
            ["itemId"] = Guid.NewGuid().ToString(), ["skuId"] = Guid.NewGuid().ToString(), ["skuLevel"] = "Lsku",
            ["quantity"] = "3", ["uomId"] = "EA", ["lotId"] = Guid.NewGuid().ToString("D")
        });
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Inventory/StockMovements/Edit.cshtml",
            StockMovementFormViewModel.ForCorrection(row), "inventory.stock.read", "inventory.movements.create");
        var original = html[html.IndexOf("id=\"movement-original\"", StringComparison.Ordinal)..];
        Assert.DoesNotContain("badge bg-label-secondary ms-1", original[..original.IndexOf("</section>", StringComparison.Ordinal)], StringComparison.Ordinal);
    }
}
