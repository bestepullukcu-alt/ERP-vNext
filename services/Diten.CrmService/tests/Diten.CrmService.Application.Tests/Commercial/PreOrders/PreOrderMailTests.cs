using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
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
/// WP-VW-W6 W6-BE-c1 — the PRODUCTION notifier (<see cref="PlatformPreOrderNotifier"/>) and transmitter
/// (<see cref="PreOrderTransmitter"/>) inside the MOD-0168 flow: the seven events to the right people at the right moment,
/// one send per (order, event, round) through the Platform's idempotency key, an e-mail failure that never breaks the
/// flow, the external recipients' language (selling company's country), the transmission frame with its retry schedule
/// (injected clock, budget, the audited scheduler command), and the cancellation mail to the depot / pharmacy only after
/// a transmission. Doubled: the Platform internal dispatch (it de-duplicates by key like the real one) and the document
/// producer (W6-BE-c2's port).
/// </summary>
public sealed class PreOrderMailTests : PreOrderTestBase
{
    private static readonly DedupingPlatform Platform = new();

    public PreOrderMailTests() : base(Wire(Platform, new FakeDocuments()))
    {
        Platform.Reset();
    }

    private PreOrderMailTests(Action<IServiceCollection> configure) : base(configure)
    {
        Platform.Reset();
    }

    /// <summary>The real notifier + transmitter over the fake Platform; <paramref name="documents"/> null = no producer (c1's
    /// production wiring).</summary>
    internal static Action<IServiceCollection> Wire(DedupingPlatform platform, FakeDocuments? documents) => services =>
    {
        services.AddSingleton<IPlatformNotificationClient>(platform);
        services.AddSingleton<IPreOrderLinks>(new ConfigurationPreOrderLinks(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Crm:Notifications:WebBaseUrl"] = "https://web.test" }).Build()));
        services.AddScoped<PlatformPreOrderNotifier>();
        services.AddScoped<IPreOrderNotifier>(sp => sp.GetRequiredService<PlatformPreOrderNotifier>());
        services.AddScoped<IPreOrderTransmissionMailer>(sp => sp.GetRequiredService<PlatformPreOrderNotifier>());
        if (documents is not null)
        {
            services.AddSingleton<IPreOrderDocumentProducer>(documents);
        }

        services.AddScoped<IPreOrderTransmitter, PreOrderTransmitter>();
    };

    private static PreOrderMailTests NotWired() => new(Wire(Platform, null));

    // ── the seven events: who, when ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task E1_a_submission_that_needs_approval_mails_the_resolved_approvers_the_signed_in_link_and_replies_to_the_rep()
    {
        var pending = await Pending();

        var mail = Assert.Single(Platform.Requests);
        Assert.Equal(PreOrderNotificationEvents.ApprovalRequested, mail.EventCode);
        Assert.Equal(new[] { PreOrderWorld.Manager.ToString("D"), PreOrderWorld.Manager2.ToString("D") }, mail.To.Select(r => r.UserId));
        Assert.All(mail.To, r => Assert.Null(r.Locale));                                   // internal users: their own language
        Assert.Equal(PreOrderWorld.Rep.ToString("D"), mail.ReplyToUserId);
        Assert.Equal(PreOrderNotificationEvents.SourceModule, mail.SourceModule);
        Assert.StartsWith("https://web.test/Commercial/PreOrders/ApprovalLink?token=", mail.Variables["approvalUrl"]);
        // W6-FIX-a (G-13) — the link names its tenant, so a signed-out approver signs in to the right one
        Assert.EndsWith("&tenantId=" + mail.TenantId.ToString("D"), mail.Variables["approvalUrl"]);
        Assert.EndsWith("/Commercial/PreOrders/Details/" + pending.Id.ToString("D") + "?tenantId=" + mail.TenantId.ToString("D"), mail.Variables["orderUrl"]);
        Assert.Equal((pending.Code, "Eczane Şifa"), (mail.Variables["orderCode"], mail.Variables["pharmacyName"]));
        Assert.Equal($"preorder:{pending.Id:N}:approvalrequested:1", mail.IdempotencyKey);
        Assert.DoesNotContain(mail.Variables.Keys, k => k.Contains("discount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task E2_an_automatic_approval_tells_the_rep_with_the_manager_in_copy_then_the_depot_and_the_pharmacy_get_their_mails()
    {
        await SetMode(PreOrderApprovalModes.AutoWithinLimits);
        var draft = await Draft(input: Input(lines: Line("TUT", 20, (10, 2))));
        await Sign(draft.Id);
        var order = Ok(await Submit(draft.Id));

        Assert.Equal(PreOrderStatuses.Transmitted, order.Status);
        var events = Platform.Requests.Select(r => r.EventCode).ToList();
        Assert.Equal(new[] { PreOrderNotificationEvents.AutoApproved, PreOrderNotificationEvents.TransmittedDepot, PreOrderNotificationEvents.PharmacyCopy }, events);
        var auto = Platform.Requests[0];
        Assert.Equal((PreOrderWorld.Rep.ToString("D"), PreOrderWorld.Rep.ToString("D")), (auto.To.Single().UserId, auto.Cc.Single().LineManagerOf));
        var depot = Platform.Requests[1];
        Assert.Equal(("siparis@selcuk.test", "tr"), (depot.To.Single().Email, depot.To.Single().Locale)); // TR company → tr
        Assert.Empty(depot.Variables["orderUrl"]);                                                       // external: no CRM link
        Assert.Equal("", depot.Variables.GetValueOrDefault("repName", ""));
        Assert.Equal(("info@sifa.test", "tr"), (Platform.Requests[2].To.Single().Email, Platform.Requests[2].To.Single().Locale));
        var stored = W.Stored(order.Id);
        Assert.NotNull(stored.Transmission!.DepotDocumentRef);
        Assert.NotNull(stored.Transmission.PharmacyCopyRef);
    }

    [Fact]
    public async Task E3_changes_requested_and_a_rejection_go_to_the_rep_with_the_reason()
    {
        var first = await Pending();
        Ok(await Decide(first.Id, PreOrderDecisions.RequestChanges, "MF too high"));
        var second = await Pending();
        Ok(await Decide(second.Id, PreOrderDecisions.Reject, "Not this month"));

        var changes = Platform.Requests.Single(r => r.EventCode == PreOrderNotificationEvents.ChangesRequested);
        var rejected = Platform.Requests.Single(r => r.EventCode == PreOrderNotificationEvents.Rejected);
        Assert.Equal((PreOrderWorld.Rep.ToString("D"), "MF too high"), (changes.To.Single().UserId, changes.Variables["reason"]));
        Assert.Equal((PreOrderWorld.Rep.ToString("D"), "Not this month"), (rejected.To.Single().UserId, rejected.Variables["reason"]));
        Assert.StartsWith("https://web.test/Commercial/PreOrders/Details/", rejected.Variables["orderUrl"]);
        Assert.EndsWith("?tenantId=" + rejected.TenantId.ToString("D"), rejected.Variables["orderUrl"]);
    }

    [Fact]
    public async Task E4_an_approval_transmits_the_depot_mail_and_the_pharmacy_copy_and_a_pharmacy_without_e_mail_gets_none()
    {
        var approved = await Approved();
        Assert.Equal(PreOrderStatuses.Transmitted, approved.Status);
        Assert.Contains(Platform.Requests, r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot);
        Assert.Contains(Platform.Requests, r => r.EventCode == PreOrderNotificationEvents.PharmacyCopy);

        Platform.Reset();
        var noMail = await Pending(input: Input(pharmacy: PreOrderWorld.Pharmacy2));
        Ok(await Decide(noMail.Id, PreOrderDecisions.Approve));
        Assert.Equal(PreOrderStatuses.Transmitted, W.Stored(noMail.Id).Status);
        Assert.DoesNotContain(Platform.Requests, r => r.EventCode == PreOrderNotificationEvents.PharmacyCopy);
    }

    // ── one send per key; a failure never breaks the flow ────────────────────────────────────────────────────────

    [Fact]
    public async Task K1_the_same_event_and_attempt_is_one_send_and_another_attempt_is_another()
    {
        var approved = await Approved();
        var order = W.Stored(approved.Id);
        var transmitter = W.Get<IPreOrderTransmitter>();
        var opened = Platform.Opened;

        await transmitter.TransmitAsync(order, 1, default); // attempt 1 again (a second scheduler, a retried call)
        Assert.Equal(opened, Platform.Opened);

        await transmitter.TransmitAsync(order, 2, default);
        Assert.Equal(opened + 2, Platform.Opened);
        var keys = Platform.Requests.Where(r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot).Select(r => r.IdempotencyKey).Distinct().ToList();
        Assert.Equal(new[] { $"preorder:{approved.Id:N}:transmitteddepot:1", $"preorder:{approved.Id:N}:transmitteddepot:2" }, keys);
    }

    [Fact]
    public async Task K2_with_the_platform_down_every_decision_stands_and_only_the_transmission_fails()
    {
        Platform.Down = true;

        var pending = await Pending();
        Assert.Equal(PreOrderStatuses.PendingApproval, pending.Status);                   // the approval mail failed — the submission stands
        var rejected = await Pending();
        Assert.Equal(PreOrderStatuses.Rejected, Ok(await Decide(rejected.Id, PreOrderDecisions.Reject, "No")).Status);
        var approved = Ok(await Decide(pending.Id, PreOrderDecisions.Approve));

        Assert.Equal(PreOrderStatuses.TransmissionFailed, approved.Status);
        Assert.Equal(PreOrderDecisions.Approve, approved.Approval!.Decision);                // the approval is never taken back (K-N8)
        Assert.Equal(VisitNotificationNotesUncertain, W.Stored(approved.Id).Transmission!.Attempts.Single().ReasonCode);
    }

    private const string VisitNotificationNotesUncertain = Diten.CrmService.Domain.Entities.VisitNotificationNotes.PlatformUncertain;

    // ── the retry ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task R1_a_failed_transmission_is_retried_by_the_scheduler_after_a_growing_wait_and_then_succeeds()
    {
        Platform.FailEvent = PreOrderNotificationEvents.TransmittedDepot;
        var failed = await Approved();
        var stored = W.Stored(failed.Id);
        Assert.Equal(PreOrderStatuses.TransmissionFailed, stored.Status);
        Assert.Equal((W.Clock.Now + TimeSpan.FromMinutes(5)).UtcTicks, stored.Transmission!.NextAttemptAtTicks);

        // not due yet → the scheduler's command does nothing
        Assert.False(Ok(await W.Send(new RunPreOrderTransmissionRetryCommand(failed.Id))));
        W.Clock.Now += TimeSpan.FromMinutes(5);
        Assert.True(Ok(await W.Send(new RunPreOrderTransmissionRetryCommand(failed.Id))));           // still failing: the wait grows
        Assert.Equal((W.Clock.Now + TimeSpan.FromMinutes(10)).UtcTicks, W.Stored(failed.Id).Transmission!.NextAttemptAtTicks);

        Platform.FailEvent = null;
        W.Clock.Now += TimeSpan.FromMinutes(10);
        Assert.True(Ok(await W.Send(new RunPreOrderTransmissionRetryCommand(failed.Id))));
        var sent = W.Stored(failed.Id);
        Assert.Equal(PreOrderStatuses.Transmitted, sent.Status);
        Assert.Null(sent.Transmission!.NextAttemptAtTicks);
        Assert.Equal(new[] { "failed", "failed", "sent" }, sent.Transmission.Attempts.Select(a => a.Status));
        // the scheduler's runs are audited as their own command
        Assert.Equal(3, W.Audit.Bodies.Count(b => JsonDocument.Parse(b).RootElement.GetProperty("requestType").GetString() == nameof(RunPreOrderTransmissionRetryCommand)));
    }

    [Fact]
    public async Task R2_the_tenant_budget_ends_the_automatic_retries_and_sales_operations_can_still_retry_by_hand()
    {
        W.Settings.Store.Single().TransmissionRetryMax = 1;
        Platform.FailEvent = PreOrderNotificationEvents.TransmittedDepot;
        var failed = await Approved();
        W.Clock.Now += TimeSpan.FromMinutes(5);
        Ok(await W.Send(new RunPreOrderTransmissionRetryCommand(failed.Id)));

        Assert.Null(W.Stored(failed.Id).Transmission!.NextAttemptAtTicks);                  // two failures > a budget of one retry
        Assert.Empty(await W.Get<IPreOrderRepository>().ListDueTransmissionRetriesAsync(DateTimeOffset.MaxValue.UtcTicks, 50, default));

        Platform.FailEvent = null;
        var retried = Ok(await W.Send(new TransmitPreOrderCommand(failed.Id, new PreOrderVersionInput()), PreOrderWorld.Ops));
        Assert.Equal(PreOrderStatuses.Transmitted, retried.Status);
    }

    [Fact]
    public async Task R3_the_hosted_scheduler_pins_each_orders_tenant_and_runs_the_due_ones_only()
    {
        Platform.FailEvent = PreOrderNotificationEvents.TransmittedDepot;
        var due = await Approved();
        var later = await Approved();
        W.Clock.Now += TimeSpan.FromMinutes(5);
        W.Orders.Store.Single(o => o.Id == later.Id).Transmission!.NextAttemptAtTicks = (W.Clock.Now + TimeSpan.FromHours(1)).UtcTicks;
        Platform.FailEvent = null;

        var services = new ServiceCollection();
        services.AddSingleton<IPreOrderRepository>(W.Orders);
        services.AddScoped<TenantContext>();
        var seen = new List<(Guid? Tenant, Guid Id)>();
        services.AddScoped<IMediator>(sp => new RecordingMediator(sp.GetRequiredService<TenantContext>(), seen, W));
        using var provider = services.BuildServiceProvider();
        var scheduler = new PreOrderTransmissionHostedService(provider.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(), NullLogger<PreOrderTransmissionHostedService>.Instance, W.Clock);

        Assert.Equal(1, await scheduler.TickAsync(default));
        Assert.Equal(new[] { ((Guid?)PreOrderWorld.Tenant, due.Id) }, seen);
        Assert.Equal(PreOrderStatuses.Transmitted, W.Stored(due.Id).Status);
        Assert.Equal(PreOrderStatuses.TransmissionFailed, W.Stored(later.Id).Status);
    }

    [Fact]
    public async Task R4_with_no_document_producer_the_transmitter_is_off_and_the_order_waits_in_approved()
    {
        using var t = NotWired();
        var approved = await t.Approved();

        Assert.Equal(PreOrderStatuses.Approved, approved.Status);
        Assert.Null(t.W.Stored(approved.Id).Transmission);
        Assert.DoesNotContain(Platform.Requests, r => r.EventCode is PreOrderNotificationEvents.TransmittedDepot or PreOrderNotificationEvents.PharmacyCopy);
    }

    // ── the cancellation (AC-FLOW-8) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task C1_cancelling_a_transmitted_order_mails_the_depot_and_the_pharmacy_each_on_its_own_and_the_rep_with_the_manager()
    {
        var transmitted = await Approved();
        Platform.Reset();

        Ok(await W.Send(new CancelPreOrderCommand(transmitted.Id, new PreOrderCancelInput("Pharmacy closed")), PreOrderWorld.Rep));

        var mails = Platform.Requests.Where(r => r.EventCode == PreOrderNotificationEvents.Cancelled).ToList();
        Assert.Equal(3, mails.Count);
        Assert.Equal((PreOrderWorld.Rep.ToString("D"), PreOrderWorld.Rep.ToString("D")), (mails[0].To.Single().UserId, mails[0].Cc.Single().LineManagerOf));
        Assert.Equal(("siparis@selcuk.test", "tr"), (mails[1].To.Single().Email, mails[1].To.Single().Locale));
        Assert.Equal(("info@sifa.test", "tr"), (mails[2].To.Single().Email, mails[2].To.Single().Locale));
        Assert.All(mails[1..], m => Assert.Empty(m.Cc));
        Assert.All(mails, m => Assert.Equal("Pharmacy closed", m.Variables["reason"]));
        Assert.Equal(3, mails.Select(m => m.IdempotencyKey).Distinct().Count());
    }

    [Fact]
    public async Task C2_cancelling_an_order_that_was_never_transmitted_mails_only_the_rep_and_the_manager()
    {
        Platform.FailEvent = PreOrderNotificationEvents.TransmittedDepot;
        var failed = await Approved();
        Platform.Reset();

        Ok(await W.Send(new CancelPreOrderCommand(failed.Id, new PreOrderCancelInput("Wrong depot")), PreOrderWorld.Rep));

        var mail = Assert.Single(Platform.Requests);
        Assert.Equal(PreOrderNotificationEvents.Cancelled, mail.EventCode);
        Assert.Equal(PreOrderWorld.Rep.ToString("D"), mail.To.Single().UserId);
        Assert.DoesNotContain(Platform.Requests, r => r.To.Any(t => t.Email is not null));
    }

    // ── the language of an external recipient (decision B) ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("TR", "tr")]
    [InlineData("FR", "fr")]
    [InlineData("ES", "es")]
    [InlineData("RU", "ru")]
    [InlineData("CN", "zh")]
    [InlineData("AE", "ar")]
    [InlineData("SA", "ar")]
    [InlineData("EG", "ar")]
    [InlineData("KZ", "ru")]
    [InlineData("KG", "ru")]
    [InlineData("BY", "ru")]
    [InlineData("UZ", "ru")]
    [InlineData("TJ", "ru")]
    [InlineData("TM", "ru")]
    [InlineData("AZ", "ru")]
    [InlineData("GE", "ru")]
    [InlineData("UA", "ru")]
    [InlineData("md", "ru")]
    [InlineData("ro", null)]
    [InlineData("PL", null)]
    [InlineData("XK", null)]
    [InlineData("CH", null)]
    [InlineData("DE", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void L1_the_selling_companys_country_decides_the_external_language_and_others_take_the_tenants(string? country, string? language)
        => Assert.Equal(language, ExternalRecipientLanguages.ForCountry(country));

    [Fact]
    public async Task L2_an_ae_company_writes_to_its_depot_in_arabic_and_a_kg_company_in_russian()
    {
        await SetMode(PreOrderApprovalModes.AutoWithinLimits);
        foreach (var (rep, sku, language) in new[] { (PreOrderWorld.RepAe, "TUT", "ar"), (PreOrderWorld.RepKg, "GLU", (string?)"ru") })
        {
            Platform.Reset();
            var draft = await Draft(rep, Input(lines: Line(sku, 30)));
            await Sign(draft.Id, rep);
            var submitted = Ok(await Submit(draft.Id, rep));
            if (submitted.Status != PreOrderStatuses.Transmitted)
            {
                Ok(await Decide(submitted.Id, PreOrderDecisions.Approve));
            }

            var depot = Platform.Requests.Single(r => r.EventCode == PreOrderNotificationEvents.TransmittedDepot);
            Assert.Equal(language, depot.To.Single().Locale);
            // the rep and the manager are internal: never given the company's language
            Assert.All(Platform.Requests.SelectMany(r => r.To.Concat(r.Cc)).Where(r => r.Email is null), r => Assert.Null(r.Locale));
        }
    }

    // ── doubles ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The Platform's internal dispatch as W5-FIX2 made it: one dispatch per idempotency key, the same answer for a
    /// repeat. <see cref="Down"/> = unreachable (uncertain); <see cref="FailEvent"/> = that event's calls fail.</summary>
    internal sealed class DedupingPlatform : IPlatformNotificationClient
    {
        private readonly Dictionary<string, PlatformNotificationResult> _answers = new(StringComparer.Ordinal);
        public List<PlatformNotificationRequest> Requests { get; } = new();
        public bool Down { get; set; }
        public string? FailEvent { get; set; }
        public int Opened { get; private set; }

        public void Reset()
        {
            lock (Requests)
            {
                Requests.Clear();
                _answers.Clear();
                Down = false;
                FailEvent = null;
                Opened = 0;
            }
        }

        public Task<PlatformNotificationResult> DispatchAsync(PlatformNotificationRequest request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request);
                if (Down || request.EventCode == FailEvent)
                {
                    return Task.FromResult(PlatformNotificationResult.Unavailable(Diten.CrmService.Domain.Entities.VisitNotificationNotes.PlatformUncertain));
                }

                if (request.IdempotencyKey is { } key && _answers.TryGetValue(key, out var stored))
                {
                    return Task.FromResult(stored);
                }

                Opened++;
                var answer = new PlatformNotificationResult(true, "queued", [Guid.NewGuid().ToString("D")], [], []);
                if (request.IdempotencyKey is { } k)
                {
                    _answers[k] = answer;
                }

                return Task.FromResult(answer);
            }
        }
    }

    internal sealed class FakeDocuments : IPreOrderDocumentProducer
    {
        public Task<PreOrderDocuments> ProduceAsync(PreOrder order, int attemptNo, CancellationToken ct)
            => Task.FromResult(new PreOrderDocuments(true, Guid.NewGuid(), Guid.NewGuid()));
    }

    /// <summary>The scheduler's mediator: records the tenant pinned for each command and runs it in the world.</summary>
    private sealed class RecordingMediator(TenantContext tenant, List<(Guid? Tenant, Guid Id)> seen, PreOrderWorld world) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var command = (RunPreOrderTransmissionRetryCommand)(object)request;
            seen.Add((tenant.TenantId, command.Id));
            return (TResponse)(object)await world.Send(command);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
