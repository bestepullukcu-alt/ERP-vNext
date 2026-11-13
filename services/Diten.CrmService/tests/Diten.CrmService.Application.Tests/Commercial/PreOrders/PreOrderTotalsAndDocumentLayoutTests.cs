using System.Net;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Infrastructure.Commercial;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-FIX-c — the order totals carry the list subtotal and the discount amount (V-3 / V-15; MOD-0156's arithmetic,
/// completed on read for an order saved before), and the depot document matches mockup E: the seller's letterhead from MDM
/// (V-13), the eight line columns with "—" for an empty cell (V-14), the totals (V-15), delivery date / currency / depot
/// e-mail (V-16), times in the selling company's country zone, named (V-17), the pharmacy's province (V-18), and no
/// transmission attempt on the depot copy (V-19). On the PRODUCTION template, producer and MDM client.
/// </summary>
public sealed class PreOrderTotalsAndDocumentLayoutTests : PreOrderTestBase
{
    private static readonly PreOrderMailTests.DedupingPlatform Platform = new();
    private static readonly PreOrderDocumentTests.FakeDocumentStore Store = new();
    private static readonly PreOrderDocumentTests.RecordingRenderer Renderer = new();

    public PreOrderTotalsAndDocumentLayoutTests() : base(Wire)
    {
        Platform.Reset();
        Store.Reset();
        Renderer.Reset();
        W.Mdm.LegalEntities[PreOrderWorld.LeTr] = new PreOrderLegalEntity(PreOrderWorld.LeTr, "GM-TR", "Grand Medical TR", "TR", "TRY",
            "Büyükdere Cad. 185, 34394 İstanbul, TR", "4560012345");
    }

    private static void Wire(IServiceCollection services)
    {
        PreOrderMailTests.Wire(Platform, null)(services);
        services.AddSingleton<IPreOrderDocumentRenderer>(Renderer);
        services.AddSingleton<IPreOrderDocumentStore>(Store);
        services.AddSingleton<IPreOrderDocumentSettings>(new ConfigurationPreOrderDocumentSettings(new ConfigurationBuilder().Build()));
        services.AddScoped<IPreOrderDocumentProducer, PreOrderDocumentProducer>();
        services.AddSingleton<IReferenceDataCatalogReader>(new Cities());
    }

    // ── the totals (V-3 / V-15) ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T1_an_mf_and_discount_line_carries_the_list_subtotal_and_the_discount_amount()
    {
        // TUT 20 × 412.60 (pharmacist purchase), 10+2, 5 %: list 8 252.00, net 7 839.40, discount 412.60
        var totals = (await Pending()).Totals!;

        Assert.Equal(((decimal?)8252.00m, (decimal?)412.60m, 7839.40m), (totals.ListSubtotal, totals.DiscountAmount, totals.Net));
        Assert.Equal((783.94m, 8623.34m), (totals.Vat, totals.Gross));                    // unchanged: VAT on the net
        Assert.Equal(totals.ListSubtotal - totals.Net, totals.DiscountAmount);
    }

    [Fact]
    public async Task T2_a_line_without_discount_has_a_zero_discount_amount_free_boxes_are_not_in_the_subtotal()
    {
        var totals = (await Pending(input: Input(lines: Line("TUT", 20, (10, 2))))).Totals!;

        Assert.Equal(((decimal?)8252.00m, (decimal?)0m, 8252.00m), (totals.ListSubtotal, totals.DiscountAmount, totals.Net));
        Assert.True(totals.EffectiveDiscountPct > 0m);                                    // the MF still makes an effective discount
    }

    [Fact]
    public async Task T3_several_lines_add_up_and_the_preview_answers_the_same_numbers()
    {
        var lines = new[] { Line("TUT", 20, (10, 2), 5m), Line("ALM", 30, null, 10m), Line("GLU", 60, (20, 3)) };
        var submitted = await Pending(input: Input(lines: lines));

        // 8 252.00 + 5 592.00 + 5 925.00 = 19 769.00; net 7 839.40 + 5 032.80 + 5 925.00 = 18 797.20
        Assert.Equal(((decimal?)19769.00m, (decimal?)971.80m, 18797.20m), (submitted.Totals!.ListSubtotal, submitted.Totals.DiscountAmount, submitted.Totals.Net));

        var preview = Ok(await W.Send(new PreviewPreOrderQuery(new PreOrderPreviewInput(PreOrderWorld.Pharmacy, lines)), PreOrderWorld.Rep));
        Assert.Equal((19769.00m, 971.80m, 18797.20m), (preview.Totals!.ListSubtotal, preview.Totals.DiscountAmount, preview.Totals.Net));
    }

    [Fact]
    public async Task T4_an_order_saved_before_the_fix_is_completed_on_read_and_nothing_is_written()
    {
        var pending = await Pending();
        W.Orders.Store.Single(o => o.Id == pending.Id).Totals!.ListSubtotal = null;            // as stored before W6-FIX-c
        W.Orders.Store.Single(o => o.Id == pending.Id).Totals!.DiscountAmount = null;
        var version = W.Stored(pending.Id).Version;

        var read = Ok(await W.Send(new GetPreOrderByIdQuery(pending.Id), PreOrderWorld.Rep));

        Assert.Equal(((decimal?)8252.00m, (decimal?)412.60m), (read.Totals!.ListSubtotal, read.Totals.DiscountAmount));
        Assert.Null(W.Stored(pending.Id).Totals!.ListSubtotal);                                 // no migration
        Assert.Equal(version, W.Stored(pending.Id).Version);
        var model = PreOrderDocumentRules.Build(W.Stored(pending.Id), PreOrderDocumentCopies.Depot, "tr", null);
        Assert.Equal((8252.00m, 412.60m), (model.Totals!.ListSubtotal, model.Totals.DiscountAmount));
    }

    // ── the document (V-13 … V-19) ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task E1_the_depot_copy_has_the_letterhead_the_eight_columns_the_totals_and_local_times_and_no_attempt_or_effective_pct()
    {
        await Approved(delivery: true);
        var depot = Renderer.Text(PreOrderDocumentCopies.Depot);

        // V-13 the letterhead (MDM legal entity)
        Assert.Equal("Grand Medical TR", depot[1]);
        Assert.Contains("Büyükdere Cad. 185, 34394 İstanbul, TR", depot);
        Assert.Contains("VKN: 4560012345", depot);
        Assert.DoesNotContain(depot, t => t.StartsWith("Satıcı:", StringComparison.Ordinal));
        // V-14 the columns, in order, each once; an empty cell is "—"
        var headers = new[] { "Ürün", "Barkod", "Adet", "MF", "İskonto %", "Birim fiyat", "KDV %", "Satır toplamı" };
        Assert.Equal(headers, depot.Where(headers.Contains).Take(headers.Length));
        Assert.Contains("—", depot);                                                            // no GTIN in v1.1
        Assert.Contains("10+2 (+4)", depot);
        Assert.Contains("5 %", depot);
        // V-15 the totals
        Assert.Contains("Ara toplam (KDV hariç, liste): 8.252,00 TRY", depot);
        Assert.Contains("İskonto: 412,60 TRY", depot);
        Assert.Contains("Net: 7.839,40 TRY", depot);
        // V-16 delivery date, currency, depot e-mail
        Assert.Contains("Teslim tarihi: 24.10.2026", depot);
        Assert.Contains("Para birimi: TRY", depot);
        Assert.Contains("Depo e-postası: siparis@selcuk.test", depot);
        // V-17 local times, named: 09:00 UTC is 12:00 in Istanbul
        Assert.Contains("İmzalayan: Ecz. Ali Veli · 21.10.2026 12:00 (Europe/Istanbul)", depot);
        Assert.Contains(depot, t => t.StartsWith("Onaylayan: ", StringComparison.Ordinal) && t.EndsWith(" · 21.10.2026 12:00 (Europe/Istanbul)", StringComparison.Ordinal));
        Assert.DoesNotContain(depot, t => t.Contains("UTC", StringComparison.Ordinal) || t.Contains("09:00", StringComparison.Ordinal));
        // V-18 the pharmacy's province
        Assert.Contains("Adres: Bağdat Cad. 1 / İstanbul", depot);
        // V-19 + DESIGN-GAP §3: no transmission attempt, no effective %
        Assert.DoesNotContain(depot, t => t.Contains("deneme", StringComparison.OrdinalIgnoreCase) || t.Contains("Etkin indirim", StringComparison.Ordinal));
    }

    [Fact]
    public async Task E2_the_pharmacy_copy_keeps_the_effective_discount_and_the_same_totals()
    {
        await Approved(delivery: true);
        var pharmacy = Renderer.Text(PreOrderDocumentCopies.Pharmacy);

        Assert.Contains(pharmacy, t => t.StartsWith("Etkin indirim %: ", StringComparison.Ordinal));
        Assert.Contains("Ara toplam (KDV hariç, liste): 8.252,00 TRY", pharmacy);
        Assert.Contains("İskonto: 412,60 TRY", pharmacy);
        Assert.Contains("VKN: 4560012345", pharmacy);
    }

    [Fact]
    public async Task E3_no_delivery_date_no_line_and_the_zone_follows_the_selling_companys_country()
    {
        var order = W.Stored((await Approved()).Id);

        var tr = new PreOrderDocumentRenderer().Render(PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, "tr", null)).Text;
        Assert.DoesNotContain(tr, t => t.StartsWith("Teslim tarihi", StringComparison.Ordinal));

        order.CountryCode = "AE";
        var model = PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, "tr", null);
        Assert.Equal("Asia/Dubai", model.TimeZone);
        Assert.Equal(TimeSpan.FromHours(4), model.SignedAt!.Value.Offset);
        Assert.Contains("İmzalayan: Ecz. Ali Veli · 21.10.2026 13:00 (Asia/Dubai)", new PreOrderDocumentRenderer().Render(model).Text);

        order.CountryCode = "ZZ";                                                               // outside the table: UTC, said so
        Assert.Contains("İmzalayan: Ecz. Ali Veli · 21.10.2026 09:00 (UTC)",
            new PreOrderDocumentRenderer().Render(PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, "tr", null)).Text);
    }

    [Fact]
    public async Task E4_a_quantity_mode_order_keeps_the_five_quantity_columns_and_prints_no_amount()
    {
        var pending = await Pending(PreOrderWorld.RepKg, Input(lines: Line("GLU", 40, (20, 3))));
        var order = W.Stored(Ok(await Decide(pending.Id, PreOrderDecisions.Approve)).Id);

        var text = new PreOrderDocumentRenderer().Render(PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, "tr", null)).Text;
        Assert.Equal(new[] { "Ürün", "Barkod", "Adet", "MF", "İskonto %" }, text.Where(t => t is "Ürün" or "Barkod" or "Adet" or "MF" or "İskonto %"));
        Assert.DoesNotContain(text, t => t is "Birim fiyat" or "Satır toplamı" || t.StartsWith("Ara toplam", StringComparison.Ordinal)
                                         || t.StartsWith("Para birimi", StringComparison.Ordinal));
        Assert.Contains("Yalnız adet — fiyatı depo uygular.", text);
        Assert.Equal("Asia/Bishkek", PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, "tr", null).TimeZone);
    }

    [Fact]
    public void E5_the_line_table_fits_the_printable_width_of_a4_in_both_modes()
    {
        foreach (var priced in new[] { true, false })
        {
            var columns = PreOrderDocumentRenderer.LineColumns(priced);
            Assert.True(columns.Sum(c => c.WidthCm) <= PreOrderDocumentRenderer.ContentWidthCm,
                $"priced={priced}: {columns.Sum(c => c.WidthCm)} cm > {PreOrderDocumentRenderer.ContentWidthCm} cm");
            Assert.Equal(priced ? 8 : 5, columns.Count);
        }
    }

    [Fact]
    public async Task E6_a_long_product_name_wraps_inside_its_column_the_page_still_renders()
    {
        var order = W.Stored((await Approved()).Id);
        order.Lines[0].Sku.Name = string.Join(" ", Enumerable.Repeat("ÇOKUZUNÜRÜNADI", 12));
        order.Lines[0].Sku.Gtin = "08699999999999";

        var rendered = new PreOrderDocumentRenderer().Render(PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Pharmacy, "tr", PreOrderDocumentTests.Png1x1));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(rendered.Pdf, 0, 5));
        Assert.Contains("08699999999999", rendered.Text);
        Assert.True(rendered.Pdf.Length <= PreOrderDocumentRules.MaxBytes);
    }

    // ── the MDM letterhead read (V-13) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task M1_the_mdm_client_reads_the_registered_address_and_the_tax_id_of_the_legal_entity()
    {
        var json = """
            {"isSuccessful":true,"data":{"code":"GM-TR","legalName":"Grand Medical Sağlık A.Ş.","displayName":"Grand Medical TR","countryCode":"tr",
              "baseCurrencyCode":"try","taxId":"4560012345","vatNumber":"TR4560012345",
              "registeredAddressJson":"{\"line1\":\"Büyükdere Cad. 185\\nKat 7\",\"postalCode\":\"34394\",\"city\":\"İstanbul\",\"country\":\"TR\"}"}}
            """;
        var le = await Directory(json).GetLegalEntityAsync(PreOrderWorld.LeTr, default);

        Assert.Equal(PreOrderLookupOutcome.Found, le.Outcome);
        Assert.Equal(("Grand Medical TR", "TR", "TRY"), (le.LegalEntity!.Name, le.LegalEntity.CountryCode, le.LegalEntity.BaseCurrencyCode));
        Assert.Equal("Büyükdere Cad. 185 Kat 7, 34394 İstanbul, TR", le.LegalEntity.Address);
        Assert.Equal("4560012345", le.LegalEntity.TaxNumber);
    }

    [Theory]
    [InlineData("""{"code":"X","countryCode":"TR","baseCurrencyCode":"TRY","registeredAddressJson":"","vatNumber":"RO123"}""", null, "RO123")]
    [InlineData("""{"code":"X","countryCode":"TR","baseCurrencyCode":"TRY","registeredAddressJson":"not json"}""", null, null)]
    [InlineData("""{"code":"X","countryCode":"TR","baseCurrencyCode":"TRY"}""", null, null)]
    public async Task M2_without_an_address_or_tax_id_the_letterhead_is_the_name_alone(string data, string? address, string? tax)
    {
        var le = (await Directory("{\"data\":" + data + "}").GetLegalEntityAsync(PreOrderWorld.LeTr, default)).LegalEntity!;
        Assert.Equal((address, tax), (le.Address, le.TaxNumber));
    }

    [Fact]
    public async Task M3_the_letterhead_is_taken_again_at_submission_and_an_order_without_one_prints_the_name_alone()
    {
        var draft = await Draft();
        Assert.Equal("4560012345", W.Stored(draft.Id).Seller.TaxNumber);                       // the draft's
        W.Mdm.LegalEntities[PreOrderWorld.LeTr] = W.Mdm.LegalEntities[PreOrderWorld.LeTr] with { TaxNumber = "9990001112" };
        await Sign(draft.Id);
        Ok(await Submit(draft.Id));
        Assert.Equal("9990001112", W.Stored(draft.Id).Seller.TaxNumber);                       // the submission's

        var order = W.Stored(draft.Id);
        order.Seller.Address = null;
        order.Seller.TaxNumber = null;
        var text = new PreOrderDocumentRenderer().Render(PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, "tr", null)).Text;
        Assert.Equal("Grand Medical TR", text[1]);
        Assert.DoesNotContain(text, t => t.StartsWith("VKN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task M4_an_unknown_city_code_or_an_unpublished_set_leaves_the_address_as_it_is()
    {
        W.Accounts.Items.Single(a => a.Id == PreOrderWorld.Pharmacy).CityRef = "nowhere";
        var order = W.Stored((await Pending()).Id);
        Assert.Null(order.Pharmacy.CityName);
        Assert.Equal("Bağdat Cad. 1", PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, "tr", null).PharmacyAddress);
    }

    // ── helpers and doubles ──────────────────────────────────────────────────────────────────────────────────────

    private async Task<PreOrderDto> Approved(bool delivery)
    {
        var input = delivery ? Input() with { RequestedDeliveryDate = W.Today.AddDays(3) } : Input();
        var pending = await Pending(input: input);
        return Ok(await Decide(pending.Id, PreOrderDecisions.Approve));
    }

    private static MdmPreOrderDirectory Directory(string body)
        => new(new HttpClient(new Answer(body)), new ConfigurationBuilder().Build(), new HttpContextAccessor(), TenantFactory.Tenant(PreOrderWorld.Tenant),
            NullLogger<MdmPreOrderDirectory>.Instance);

    private sealed class Answer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    /// <summary>MOD-0048's published <c>city</c> set as the consumable route answers it.</summary>
    private sealed class Cities : IReferenceDataCatalogReader
    {
        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
            => Task.FromResult(setCode == ContactReferenceValidation.CitySet
                ? new ReferenceSetSnapshot(setCode, true, [new ReferenceValueSnapshot("istanbul", "İstanbul", null, true, false, null)])
                : ReferenceSetSnapshot.NotPublished(setCode));
    }
}
