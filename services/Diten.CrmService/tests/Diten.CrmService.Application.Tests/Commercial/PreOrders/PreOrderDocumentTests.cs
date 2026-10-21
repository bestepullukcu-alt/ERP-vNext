using System.IO.Compression;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using Diten.CrmService.Infrastructure.Commercial;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-c2 — the depot document on the PRODUCTION template (<see cref="PreOrderDocumentRenderer"/>, PDFsharp-MigraDoc)
/// and producer (<see cref="PreOrderDocumentProducer"/>) inside the MOD-0168 flow: two copies from one template (the depot copy
/// carries no discount percentage), the language of the selling company's country, the automatic approval line, no label
/// for a missing field, the 256 KB cap, the documents written once per transmission (a retry reuses the bound refs), the
/// PDFs attached by reference, the preview through the SAME template and visibility, the pharmacy copy as best effort, and
/// the approved backlog taken by the scheduler. Doubled: the Platform internal dispatch and MOD-0262 (the internal door).
/// </summary>
public sealed class PreOrderDocumentTests : PreOrderTestBase
{
    private static readonly PreOrderMailTests.DedupingPlatform Platform = new();
    private static readonly FakeDocumentStore Store = new();
    private static readonly RecordingRenderer Renderer = new();

    public PreOrderDocumentTests() : base(Wire)
    {
        Platform.Reset();
        Store.Reset();
        Renderer.Reset();
    }

    private static void Wire(IServiceCollection services)
    {
        PreOrderMailTests.Wire(Platform, null)(services);
        services.AddSingleton<IPreOrderDocumentRenderer>(Renderer);
        services.AddSingleton<IPreOrderDocumentStore>(Store);
        services.AddSingleton<IPreOrderDocumentSettings>(new ConfigurationPreOrderDocumentSettings(new ConfigurationBuilder().Build()));
        services.AddScoped<IPreOrderDocumentProducer, PreOrderDocumentProducer>();
    }

    // ── the two copies ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task D1_the_depot_copy_shows_no_discount_percentage_and_the_pharmacy_copy_does_both_say_the_depot_invoices()
    {
        var transmitted = await Approved();
        Assert.Equal(PreOrderStatuses.Transmitted, transmitted.Status);

        var depot = Renderer.Text(PreOrderDocumentCopies.Depot);
        var pharmacy = Renderer.Text(PreOrderDocumentCopies.Pharmacy);
        Assert.DoesNotContain(depot, t => t.Contains("İskonto", StringComparison.Ordinal) || t.Contains("indirim", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(depot, t => t == "5 %");
        Assert.Contains("İskonto %", pharmacy);
        Assert.Contains("5 %", pharmacy);
        Assert.Contains(pharmacy, t => t.StartsWith("Etkin indirim %: ", StringComparison.Ordinal));
        Assert.All(new[] { depot, pharmacy }, text => Assert.Contains("Bu bir ön sipariştir; faturayı depo düzenler.", text));
        Assert.Contains("Ön sipariş — depo nüshası", depot);
        Assert.Contains("Ön sipariş — eczane kopyası", pharmacy);
        Assert.Contains(depot, t => t.StartsWith("Onaylayan: Yönetici b200 ·", StringComparison.Ordinal));   // the name PLT-W6 resolved at submission
        Assert.Contains(depot, t => t.StartsWith("Not: Kasım siparişi", StringComparison.Ordinal));
        Assert.All(Store.Written, w => Assert.True(w.Pdf.Length is > 0 and <= PreOrderDocumentRules.MaxBytes));
        Assert.All(Store.Written, w => Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(w.Pdf, 0, 5)));
    }

    [Fact]
    public async Task D2_an_automatic_approval_says_so_and_names_nobody()
    {
        await SetMode(PreOrderApprovalModes.AutoWithinLimits);
        var draft = await Draft(input: Input(lines: Line("TUT", 20, (10, 2))));
        await Sign(draft.Id);
        Assert.Equal(PreOrderStatuses.Transmitted, Ok(await Submit(draft.Id)).Status);

        var text = Renderer.Text(PreOrderDocumentCopies.Depot);
        Assert.Contains("Kendiliğinden onaylandı (kiracı ayarı)", text);
        Assert.DoesNotContain(text, t => t.StartsWith("Onaylayan", StringComparison.Ordinal));
    }

    [Fact]
    public async Task D3_a_field_the_order_does_not_carry_is_not_printed_at_all_no_label_and_no_dash()
    {
        await Approved();
        foreach (var copy in PreOrderDocumentCopies.All)
        {
            var text = Renderer.Text(copy);
            Assert.DoesNotContain(text, t => t.StartsWith("GLN", StringComparison.Ordinal) || t.StartsWith("Eczane kodu", StringComparison.Ordinal)
                                             || t.StartsWith("Vergi no", StringComparison.Ordinal) || t.Contains(" · Form", StringComparison.Ordinal));
            Assert.DoesNotContain(text, t => t.Contains('—') && !t.StartsWith("Ön sipariş", StringComparison.Ordinal));
            Assert.DoesNotContain("Barkod", text);                                         // no line has a GTIN in v1.1
        }
    }

    [Theory]
    [InlineData("TR", "Ön sipariş — depo nüshası", "Bu bir ön sipariştir; faturayı depo düzenler.")]
    [InlineData("RO", "Pre-order — depot copy", "This is a pre-order; the depot issues the invoice.")]
    [InlineData("AE", "طلب مسبق — نسخة المستودع", "هذا طلب مسبق؛ يُصدر المستودع الفاتورة.")]
    [InlineData("FR", "Pré-commande — exemplaire du dépositaire", "Ceci est une pré-commande ; le dépositaire établit la facture.")]
    [InlineData("RU", "Предзаказ — экземпляр дистрибьютора", "Это предварительный заказ; счёт выставляет дистрибьютор.")]
    [InlineData("ES", "Prepedido — copia del distribuidor", "Esto es un prepedido; el distribuidor emite la factura.")]
    public async Task D4_the_document_speaks_the_selling_companys_country_language_and_another_country_the_fallback(string country, string title, string disclaimer)
    {
        var approved = await Approved();
        var order = W.Stored(approved.Id);
        order.CountryCode = country;

        var language = PreOrderDocumentRules.LanguageOf(order, "en");
        var rendered = new PreOrderDocumentRenderer().Render(PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Depot, language, null));

        Assert.Equal(title, rendered.Text[0]);
        Assert.Contains(disclaimer, rendered.Text);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(rendered.Pdf, 0, 5));
    }

    [Fact]
    public async Task D4b_every_one_of_the_seven_languages_renders_a_pdf_with_its_own_words()
    {
        var order = W.Stored((await Approved()).Id);
        var titles = new HashSet<string>();
        foreach (var language in PreOrderDocumentRenderer.SupportedLanguages)
        {
            var rendered = new PreOrderDocumentRenderer().Render(PreOrderDocumentRules.Build(order, PreOrderDocumentCopies.Pharmacy, language, Png1x1));
            Assert.True(rendered.Pdf.Length > 1000, language);
            Assert.True(titles.Add(rendered.Text[0]), $"{language}: {rendered.Text[0]} is another language's title");
        }
    }

    // ── 256 KB, the store, the reuse ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task S1_a_document_over_256_kb_fails_the_transmission_with_its_reason_and_nothing_is_written_or_mailed()
    {
        Store.Signature = NoisyPng(420, 420);                         // incompressible: the PDF grows past 256 KB
        var failed = await Approved();

        var stored = W.Stored(failed.Id);
        Assert.Equal(PreOrderStatuses.TransmissionFailed, stored.Status);
        Assert.Equal(PreOrderDocumentRules.TooLarge, stored.Transmission!.Attempts.Single().ReasonCode);
        Assert.Empty(Store.Written);
        Assert.DoesNotContain(Platform.Requests, r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot);
        Assert.NotNull(stored.Transmission.NextAttemptAtTicks);
    }

    [Fact]
    public async Task S2_a_failed_platform_write_fails_the_attempt_and_the_retry_writes_then_mails()
    {
        Store.FailWrites = 1;
        var failed = await Approved();
        Assert.Equal(PreOrderDocumentRules.StoreFailed, W.Stored(failed.Id).Transmission!.Attempts.Single().ReasonCode);

        W.Clock.Now += TimeSpan.FromMinutes(5);
        Assert.True(Ok(await W.Send(new RunPreOrderTransmissionRetryCommand(failed.Id))));
        Assert.Equal(PreOrderStatuses.Transmitted, W.Stored(failed.Id).Status);
        Assert.Equal(2, Store.Written.Count);
    }

    [Fact]
    public async Task S3_when_the_mail_fails_after_the_documents_were_written_the_retry_reuses_them_and_renders_nothing_again()
    {
        Platform.FailEvent = PreOrderNotificationEvents.TransmittedDepot;
        var failed = await Approved();
        var first = W.Stored(failed.Id).Transmission!;
        Assert.Equal((2, 2), (Store.Written.Count, Renderer.Calls.Count));
        Assert.NotNull(first.DepotDocumentRef);
        Assert.NotNull(first.PharmacyCopyRef);

        Platform.FailEvent = null;
        W.Clock.Now += TimeSpan.FromMinutes(5);
        Ok(await W.Send(new RunPreOrderTransmissionRetryCommand(failed.Id)));

        var sent = W.Stored(failed.Id).Transmission!;
        Assert.Equal((2, 2), (Store.Written.Count, Renderer.Calls.Count));          // no second render, no second MOD-0262 version
        Assert.Equal((first.DepotDocumentRef, first.PharmacyCopyRef), (sent.DepotDocumentRef, sent.PharmacyCopyRef));
        var depotMails = Platform.Requests.Where(r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot).ToList();
        Assert.All(depotMails, m => Assert.Equal(first.DepotDocumentRef, m.Attachments!.Single().DocumentRef));
    }

    // ── the attachments, the signature ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A1_the_depot_mail_and_the_pharmacy_copy_carry_their_own_pdf_by_reference_and_the_signature_is_read_without_a_user()
    {
        var transmitted = await Approved();
        var t = W.Stored(transmitted.Id).Transmission!;

        var depot = Platform.Requests.Single(r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot).Attachments!.Single();
        var pharmacy = Platform.Requests.Single(r => r.EventCode == PreOrderNotificationEvents.PharmacyCopy).Attachments!.Single();
        Assert.Equal((t.DepotDocumentRef!.Value, "application/pdf", $"{transmitted.Code}-depot.pdf"), (depot.DocumentRef, depot.ContentType, depot.FileName));
        Assert.Equal((t.PharmacyCopyRef!.Value, $"{transmitted.Code}-pharmacy.pdf"), (pharmacy.DocumentRef, pharmacy.FileName));
        Assert.Contains(W.Stored(transmitted.Id).Signature!.BinaryRef, Store.Reads);   // the CrmVisitEvidence signature, internal door
        Assert.All(Renderer.Calls, c => Assert.NotNull(c.SignatureImage));
        Assert.DoesNotContain(Platform.Requests, r => r.EventCode is PreOrderNotificationEvents.ApprovalRequested && r.Attachments is not null);
    }

    [Fact]
    public async Task A2_a_signature_that_cannot_be_read_fails_the_attempt_by_name_and_it_is_retried()
    {
        Store.FailReads = true;
        var failed = await Approved();
        var stored = W.Stored(failed.Id);
        Assert.Equal((PreOrderStatuses.TransmissionFailed, PreOrderDocumentRules.SignatureUnavailable),
            (stored.Status, stored.Transmission!.Attempts.Single().ReasonCode));
        Assert.NotNull(stored.Transmission.NextAttemptAtTicks);
    }

    // ── the pharmacy copy is best effort (FU-W6-PHARMACY-COPY-BEST-EFFORT-TEST) ──────────────────────────────────

    [Fact]
    public async Task B1_a_failed_pharmacy_copy_leaves_the_order_transmitted_opens_no_new_attempt_and_never_mails_the_depot_twice()
    {
        Platform.FailEvent = PreOrderNotificationEvents.PharmacyCopy;
        var transmitted = await Approved();

        var stored = W.Stored(transmitted.Id);
        Assert.Equal(PreOrderStatuses.Transmitted, stored.Status);
        Assert.Equal(new[] { PreOrderTransmissionStatuses.Sent }, stored.Transmission!.Attempts.Select(a => a.Status));
        Assert.Null(stored.Transmission.NextAttemptAtTicks);
        W.Clock.Now += TimeSpan.FromHours(7);
        Assert.Empty(await W.Get<IPreOrderRepository>().ListDueTransmissionRetriesAsync(W.Clock.Now.UtcTicks, 50, default));
        Assert.False(Ok(await W.Send(new RunPreOrderTransmissionRetryCommand(transmitted.Id))));
        Assert.Single(Platform.Requests, r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot);
    }

    // ── the approved backlog (FU-W6-APPROVED-BACKLOG) ────────────────────────────────────────────────────────────

    [Fact]
    public async Task K1_an_approved_order_that_never_had_an_attempt_is_transmitted_by_the_scheduler_after_the_grace()
    {
        var approved = await Approved();
        var row = W.Orders.Store.Single(o => o.Id == approved.Id);
        row.Status = PreOrderStatuses.Approved;                                         // as the transmitter-off days left it
        row.Transmission = null;
        row.Approval!.DecidedAt = W.Clock.Now - TimeSpan.FromMinutes(1);
        Platform.Reset();
        Store.Reset();

        var scheduler = Scheduler();
        Assert.Equal(0, await scheduler.TickAsync(default));                            // inside the grace: the decision transmits inline
        row.Approval.DecidedAt = W.Clock.Now - TimeSpan.FromMinutes(3);
        Assert.Equal(1, await scheduler.TickAsync(default));

        Assert.Equal(PreOrderStatuses.Transmitted, W.Stored(approved.Id).Status);
        Assert.Single(Platform.Requests, r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot);
        Assert.Equal(0, await scheduler.TickAsync(default));                            // done: never again
    }

    // ── the preview: the same template, the order's visibility ───────────────────────────────────────────────────

    [Fact]
    public async Task P1_the_preview_is_the_same_template_and_model_as_the_transmitted_pdf()
    {
        var transmitted = await Approved();
        var produced = Renderer.Calls.Single(c => c.Copy == PreOrderDocumentCopies.Pharmacy);
        Renderer.Calls.Clear();

        var file = Ok(await W.Send(new GetPreOrderDocumentQuery(transmitted.Id, "pharmacy"), PreOrderWorld.Rep));

        var previewed = Assert.Single(Renderer.Calls);                                 // through THE registered template
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
        Assert.Equal($"{transmitted.Code}-pharmacy.pdf", file.FileName);
        static string Json(PreOrderDocumentModel m) => System.Text.Json.JsonSerializer.Serialize(m with { SignatureImage = null, TransmissionAttempt = null });
        Assert.Equal(Json(produced), Json(previewed));
        Assert.NotNull(previewed.SignatureImage);                                     // the signature is read for the preview too
    }

    [Fact]
    public async Task P2_the_preview_follows_the_orders_visibility_and_refuses_an_unknown_copy()
    {
        var transmitted = await Approved();

        Assert.Equal(404, (await W.Send(new GetPreOrderDocumentQuery(transmitted.Id, "depot"), PreOrderWorld.OtherRep)).StatusCode);
        Assert.Equal(404, (await W.Send(new GetPreOrderDocumentQuery(transmitted.Id, "depot"), PreOrderWorld.Stranger)).StatusCode);
        Assert.Equal(404, (await W.Send(new GetPreOrderDocumentQuery(Guid.NewGuid(), "depot"), PreOrderWorld.Rep)).StatusCode);
        Assert.Equal(400, (await W.Send(new GetPreOrderDocumentQuery(transmitted.Id, "invoice"), PreOrderWorld.Rep)).StatusCode);
        Assert.True((await W.Send(new GetPreOrderDocumentQuery(transmitted.Id, "depot"), PreOrderWorld.Manager)).IsSuccessful);
        Assert.True((await W.Send(new GetPreOrderDocumentQuery(transmitted.Id, null), PreOrderWorld.Ops)).IsSuccessful);
    }

    // ── helpers and doubles ──────────────────────────────────────────────────────────────────────────────────────

    private PreOrderTransmissionHostedService Scheduler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPreOrderRepository>(W.Orders);
        services.AddScoped<TenantContext>();
        services.AddScoped<IMediator>(_ => new WorldMediator(W));
        return new PreOrderTransmissionHostedService(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(), NullLogger<PreOrderTransmissionHostedService>.Instance, W.Clock);
    }

    internal static readonly byte[] Png1x1 = Convert.FromBase64String(Png);

    /// <summary>A valid RGB PNG of random pixels (deflate cannot shrink it) — a signature image too big for one page.</summary>
    internal static byte[] NoisyPng(int width, int height)
    {
        var random = new Random(42);
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            var row = new byte[width * 3];
            random.NextBytes(row);
            raw.Write(row);
        }

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(z);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BigEndian(header, 0, width);
        BigEndian(header, 4, height);
        header[8] = 8;  // bit depth
        header[9] = 2;  // RGB
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        var length = new byte[4];
        BigEndian(length, 0, data.Length);
        png.Write(length);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(typed);
        var crc = new byte[4];
        BigEndian(crc, 0, unchecked((int)Crc32(typed)));
        png.Write(crc);
    }

    private static void BigEndian(byte[] target, int at, int value)
    {
        target[at] = (byte)(value >> 24);
        target[at + 1] = (byte)(value >> 16);
        target[at + 2] = (byte)(value >> 8);
        target[at + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    /// <summary>MOD-0262 through the internal door, in memory: writes are recorded; a read of a written document answers its
    /// bytes, any other ref (the pharmacist's signature) the configured image.</summary>
    internal sealed class FakeDocumentStore : IPreOrderDocumentStore
    {
        private readonly Dictionary<Guid, byte[]> _bytes = new();
        public List<(Guid Ref, string FileName, byte[] Pdf)> Written { get; } = new();
        public List<Guid> Reads { get; } = new();
        public int FailWrites { get; set; }
        public bool FailReads { get; set; }
        public byte[] Signature { get; set; } = Png1x1;

        public void Reset()
        {
            lock (Written)
            {
                _bytes.Clear();
                Written.Clear();
                Reads.Clear();
                FailWrites = 0;
                FailReads = false;
                Signature = Png1x1;
            }
        }

        public Task<Guid?> StoreAsync(Guid tenantId, Guid preOrderId, string fileName, byte[] pdf, CancellationToken ct)
        {
            lock (Written)
            {
                if (FailWrites > 0)
                {
                    FailWrites--;
                    return Task.FromResult<Guid?>(null);
                }

                var id = Guid.NewGuid();
                _bytes[id] = pdf;
                Written.Add((id, fileName, pdf));
                return Task.FromResult<Guid?>(id);
            }
        }

        public Task<byte[]?> ReadAsync(Guid tenantId, Guid documentRef, CancellationToken ct)
        {
            lock (Written)
            {
                Reads.Add(documentRef);
                return Task.FromResult<byte[]?>(FailReads ? null : _bytes.TryGetValue(documentRef, out var b) ? b : Signature);
            }
        }
    }

    /// <summary>THE production template, with what it was asked to render recorded.</summary>
    internal sealed class RecordingRenderer : IPreOrderDocumentRenderer
    {
        private readonly PreOrderDocumentRenderer _inner = new();
        private readonly Dictionary<string, IReadOnlyList<string>> _text = new();
        public List<PreOrderDocumentModel> Calls { get; } = new();

        public void Reset()
        {
            lock (Calls)
            {
                Calls.Clear();
                _text.Clear();
            }
        }

        public IReadOnlyList<string> Text(string copy)
        {
            lock (Calls)
            {
                return _text[copy];
            }
        }

        public PreOrderRenderedDocument Render(PreOrderDocumentModel model)
        {
            var rendered = _inner.Render(model);
            lock (Calls)
            {
                Calls.Add(model);
                _text[model.Copy] = rendered.Text;
            }

            return rendered;
        }
    }

    private sealed class WorldMediator(PreOrderWorld world) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => (TResponse)(object)await world.Send((RunPreOrderTransmissionRetryCommand)(object)request);

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
