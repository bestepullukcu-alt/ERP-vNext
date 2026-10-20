using System.Net;
using System.Text;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Infrastructure.Auth;
using Diten.CrmService.Infrastructure.VisitNotifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W5 · W5-FIX2 (live E4 F-1, F-3, F-6) on the PRODUCTION sender, Platform client and settings handler: every send of
/// a row carries the row id as the idempotency key; a time-out is "uncertain" — the row fails and is retried, and the
/// Platform (a fake that de-duplicates by key, like the real one) answers the FIRST call's dispatch on the retry; the
/// Platform's "queued" makes the row Sent; the settings name their last writer instead of an id.
/// </summary>
public sealed class VwW5Fix2Tests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000f2");
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private readonly W5Notifications _n = new(Now);

    [Fact]
    public async Task K1_every_send_of_a_row_carries_the_row_id_as_the_idempotency_key_on_the_wire()
    {
        var platform = new DedupingPlatform();
        var row = NewRow();

        await Sender(platform).QueueAndSendAsync(row, default);

        var body = JsonDocument.Parse(Assert.Single(platform.Bodies)).RootElement;
        Assert.Equal(row.Id.ToString("D"), body.GetProperty("idempotencyKey").GetString());
        Assert.Equal(row.Id.ToString("D"), VisitNotificationSender.IdempotencyKeyOf(row));
    }

    [Fact]
    public async Task K2_a_timeout_is_uncertain_the_row_is_retried_with_the_same_key_and_the_platform_answers_its_first_dispatch()
    {
        // The first call reaches the Platform (it stores the dispatch) but answers after CRM's 3-second budget.
        var platform = new DedupingPlatform { SlowFirstAnswer = TimeSpan.FromSeconds(4) };
        var sender = Sender(platform);
        var row = NewRow();

        await sender.QueueAndSendAsync(row, default);

        var failed = _n.Rows.Items.Single(r => r.Id == row.Id);
        Assert.Equal(VisitNotificationStatuses.Failed, failed.Status);
        Assert.Equal(VisitNotificationNotes.PlatformUncertain, failed.LastError);
        Assert.NotNull(failed.NextAttemptTicks);
        Assert.Equal(1, platform.DispatchesOpened);

        // The scheduler's retry: the same key — the Platform opens nothing new and answers the first dispatch.
        await sender.SendAsync(failed, default);

        var sent = _n.Rows.Items.Single(r => r.Id == row.Id);
        Assert.Equal(VisitNotificationStatuses.Sent, sent.Status);
        Assert.Equal(1, platform.DispatchesOpened);
        Assert.Equal(platform.FirstDispatchId, Assert.Single(sent.DispatchIds));
        Assert.Equal(2, platform.Bodies.Count);
        Assert.All(platform.Bodies, b => Assert.Equal(row.Id.ToString("D"), JsonDocument.Parse(b).RootElement.GetProperty("idempotencyKey").GetString()));
    }

    [Fact]
    public void K3_the_platforms_queued_answer_makes_the_row_sent_and_an_unavailable_one_failed_and_due()
    {
        var row = NewRow();
        VisitNotificationSender.Apply(row, new PlatformNotificationResult(true, "queued", ["d-1"], [], []), Now);
        Assert.Equal((VisitNotificationStatuses.Sent, (DateTimeOffset?)Now), (row.Status, row.SentAt));

        var other = NewRow();
        other.Attempts = 1;
        VisitNotificationSender.Apply(other, PlatformNotificationResult.Unavailable(VisitNotificationNotes.PlatformUncertain), Now);
        Assert.Equal(VisitNotificationStatuses.Failed, other.Status);
        Assert.Equal((Now + VisitNotificationLimits.Backoff(1)).UtcTicks, other.NextAttemptTicks);
    }

    [Fact]
    public async Task K4_the_settings_name_their_last_writer_and_an_unresolved_writer_is_null_never_the_id()
    {
        var writer = Guid.NewGuid();
        var audit = new RecordingAudit();
        var names = new Names { [writer] = "Ayşe Demir" };
        var h = new VisitNotificationSettingsHandlers(TenantFactory.Tenant(Tenant), new Actor(writer.ToString("D")), _n.Settings, audit, _n.Clock, names);

        var saved = (await h.Handle(new UpdateVisitNotificationSettingsCommand(new("pv@grandmedical.test", null, null, null, null)), default)).Data!;
        Assert.Equal((writer.ToString("D"), "Ayşe Demir"), (saved.UpdatedBy, saved.UpdatedByName));
        Assert.Equal("Ayşe Demir", (await h.Handle(new GetVisitNotificationSettingsQuery(), default)).Data!.UpdatedByName);

        // AuthService does not know them (or is down): no name — the screen shows "—".
        names.Clear();
        Assert.Null((await h.Handle(new GetVisitNotificationSettingsQuery(), default)).Data!.UpdatedByName);
        names.Throw = true;
        Assert.Null((await h.Handle(new GetVisitNotificationSettingsQuery(), default)).Data!.UpdatedByName);
        // Without a resolver (the default seam) the same.
        var plain = new VisitNotificationSettingsHandlers(TenantFactory.Tenant(Tenant), new Actor(writer.ToString("D")), _n.Settings, audit, _n.Clock);
        Assert.Null((await plain.Handle(new GetVisitNotificationSettingsQuery(), default)).Data!.UpdatedByName);
    }

    // ── the world ──────────────────────────────────────────────────────────────────────────────

    private VisitNotificationSender Sender(DedupingPlatform platform) => new(_n.Rows, _n.Settings,
        new PlatformNotificationClient(new HttpClient(platform),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PlatformService:BaseUrl"] = "http://platform.test" }).Build(),
            Options.Create(new AuthServiceOptions { InternalApiKey = "internal-key-1" })),
        _n.Clock);

    private static VisitNotification NewRow() => new()
    {
        Id = Guid.NewGuid(), TenantId = Tenant, EventCode = VisitNotificationEvents.AdverseEvent, SubjectId = Guid.NewGuid(), PlannedVisitId = Guid.NewGuid(),
        ResourceId = Guid.NewGuid().ToString("D"), VisitDate = DateOnly.FromDateTime(Now.UtcDateTime),
        Recipients = { new VisitNotificationRecipient { Role = VisitNotificationRoles.To, Kind = VisitNotificationRecipientKinds.LineManager, UserId = "rep-1" } },
        Variables = new Dictionary<string, string> { ["repName"] = "Okan Tekin" }
    };

    /// <summary>The Platform's internal dispatch as W5-FIX2 made it: one dispatch per idempotency key, and the same answer
    /// for a repeat. The first answer can be made slow (the call landed; the answer missed CRM's budget).</summary>
    private sealed class DedupingPlatform : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);
        public List<string> Bodies { get; } = new();
        public TimeSpan SlowFirstAnswer { get; set; }
        public int DispatchesOpened { get; private set; }
        public string? FirstDispatchId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            Bodies.Add(body);
            var key = JsonDocument.Parse(body).RootElement.TryGetProperty("idempotencyKey", out var k) ? k.GetString() : null;
            if (key is null || !_answers.TryGetValue(key, out var answer))
            {
                DispatchesOpened++;
                var id = Guid.NewGuid().ToString("D");
                FirstDispatchId ??= id;
                answer = "{\"data\":{\"dispatches\":[{\"dispatchId\":\"" + id + "\",\"locale\":\"tr\",\"status\":\"queued\"}]," +
                         "\"resolved\":{\"to\":[{\"userId\":\"mgr-1\",\"via\":\"line-manager\"}],\"cc\":[]},\"status\":\"queued\"},\"isSuccessful\":true}";
                if (key is not null)
                {
                    _answers[key] = answer;
                }

                if (SlowFirstAnswer > TimeSpan.Zero && DispatchesOpened == 1)
                {
                    await Task.Delay(SlowFirstAnswer, ct);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Actor(string name) : IActorContext
    {
        public string? ActorName => name;
    }

    private sealed class Names : Dictionary<Guid, string>, IUserDisplayNameResolver
    {
        public bool Throw { get; set; }

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
            => Throw
                ? throw new HttpRequestException("auth down")
                : Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.Where(ContainsKey).ToDictionary(id => id, id => this[id]));
    }
}
