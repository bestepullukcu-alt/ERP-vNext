using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

// `Task` here is System.Threading.Tasks.Task.
using Task = System.Threading.Tasks.Task;

/// <summary>
/// MOD-0357 S9 (owner, 2026-09-13) — <see cref="IReviewMeetingGateReader"/> against a REAL mongod: the golden
/// matrix the WP demands, run against production repositories (<see cref="RecordLinkRepository"/>,
/// <see cref="MeetingRepository"/>, <see cref="MeetingMinutesVersionRepository"/>), never fakes. Requirement
/// (Required/Optional/NotAllowed) is a TaskType concern the reader knows nothing about — that half of the golden
/// matrix (requirement × action gating) is covered at the provider/handler layer (<c>ReviewMeetingGateTests</c>);
/// this file covers every meeting/minutes SHAPE the reader itself must tell apart.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class ReviewMeetingGateReaderMongoTests
{
    [Fact]
    public async Task No_linked_meeting_at_all_is_not_unlocked()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, _, taskId) = Arrange(mongo);

        Assert.False(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task A_merely_scheduled_meeting_with_no_minutes_at_all_does_not_unlock()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var meeting = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.LinkAsync(tenantId, taskId, meeting.Id);

        Assert.False(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task Draft_minutes_do_not_unlock()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var meeting = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.LinkAsync(tenantId, taskId, meeting.Id);
        await ctx.SeedMinutesAsync(tenantId, meeting.Id, 1, MinutesStatus.Draft);

        Assert.False(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task Published_minutes_on_a_live_meeting_unlock()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var meeting = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.LinkAsync(tenantId, taskId, meeting.Id);
        await ctx.SeedMinutesAsync(tenantId, meeting.Id, 1, MinutesStatus.Published);

        Assert.True(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task A_CANCELLED_meeting_s_published_minutes_do_NOT_unlock()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var meeting = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Cancelled);
        await ctx.LinkAsync(tenantId, taskId, meeting.Id);
        await ctx.SeedMinutesAsync(tenantId, meeting.Id, 1, MinutesStatus.Published);

        Assert.False(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task A_correction_republished_as_a_new_higher_version_still_reads_as_unlocked()
    {
        // Pack rule: a correction of a published minutes is created directly as Published — the LATEST version
        // is always read, never version 1 specifically.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var meeting = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.LinkAsync(tenantId, taskId, meeting.Id);
        await ctx.SeedMinutesAsync(tenantId, meeting.Id, 1, MinutesStatus.Published);
        await ctx.SeedMinutesAsync(tenantId, meeting.Id, 2, MinutesStatus.Published);

        Assert.True(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task Multiple_linked_meetings__ONE_published_is_enough_to_unlock()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var stillScheduled = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        var published = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.LinkAsync(tenantId, taskId, stillScheduled.Id);
        await ctx.LinkAsync(tenantId, taskId, published.Id);
        await ctx.SeedMinutesAsync(tenantId, published.Id, 1, MinutesStatus.Published);

        Assert.True(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task Multiple_linked_meetings__the_ONLY_published_one_is_cancelled__stays_locked()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var cancelledPublished = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Cancelled);
        var scheduledOnly = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.LinkAsync(tenantId, taskId, cancelledPublished.Id);
        await ctx.LinkAsync(tenantId, taskId, scheduledOnly.Id);
        await ctx.SeedMinutesAsync(tenantId, cancelledPublished.Id, 1, MinutesStatus.Published);

        Assert.False(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task A_link_of_a_DIFFERENT_type_is_never_read_as_a_review_meeting()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskId) = Arrange(mongo);
        var meeting = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.SeedMinutesAsync(tenantId, meeting.Id, 1, MinutesStatus.Published);
        // Wrong link type: "agenda", not "reviewMeeting" — must not count.
        await ctx.RecordLinks.CreateAsync(new RecordLink
        {
            TenantId = tenantId,
            SourceModuleCode = RecordLinkModuleCodes.Meetings,
            SourceRecordId = meeting.Id,
            TargetModuleCode = RecordLinkModuleCodes.Tasks,
            TargetRecordId = taskId,
            LinkType = RecordLinkTypes.Agenda,
            CreatedByUserId = TaskTestData.Me,
            CreatedBy = "test"
        });

        Assert.False(await reader.HasUnlockedReviewMeetingAsync(taskId, CancellationToken.None));
    }

    [Fact]
    public async Task Batched_read_answers_several_tasks_in_one_call_without_cross_talk()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, ctx, tenantId, taskA) = Arrange(mongo);
        var taskB = Guid.NewGuid();
        var meetingA = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        var meetingB = await ctx.SeedMeetingAsync(tenantId, MeetingLifecycle.Scheduled);
        await ctx.LinkAsync(tenantId, taskA, meetingA.Id);
        await ctx.LinkAsync(tenantId, taskB, meetingB.Id);
        await ctx.SeedMinutesAsync(tenantId, meetingA.Id, 1, MinutesStatus.Published);
        // taskB's own meeting has no minutes at all — must resolve independently to "not unlocked".

        var result = await reader.ResolveUnlockedReviewMeetingsAsync([taskA, taskB], CancellationToken.None);

        Assert.True(result[taskA]);
        Assert.False(result[taskB]);
    }

    [Fact]
    public async Task A_task_absent_from_the_batched_result_is_treated_as_not_unlocked()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (reader, _, _, _) = Arrange(mongo);
        var untouchedTaskId = Guid.NewGuid();

        var result = await reader.ResolveUnlockedReviewMeetingsAsync([untouchedTaskId], CancellationToken.None);

        Assert.False(result.TryGetValue(untouchedTaskId, out var unlocked) && unlocked);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private static (IReviewMeetingGateReader Reader, MongoTestContext Ctx, Guid TenantId, Guid TaskId) Arrange(
        DisposableMongoReplicaSet mongo)
    {
        var tenantId = Guid.NewGuid();
        var database = mongo.CreateDatabase();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var dbContext = new PlatformDbContext(mongo.Client, database);

        var recordLinks = new RecordLinkRepository(dbContext, tenantContext);
        var meetings = new MeetingRepository(dbContext, tenantContext);
        var minutesVersions = new MeetingMinutesVersionRepository(dbContext, tenantContext);
        var recordLinkService = new RecordLinkService(
            recordLinks, tenantContext, new FakeCurrentUserContext(TaskTestData.Me));

        var reader = new ReviewMeetingGateReader(recordLinkService, meetings, minutesVersions);
        var ctx = new MongoTestContext(recordLinks, meetings, minutesVersions);
        var taskId = Guid.NewGuid();
        return (reader, ctx, tenantId, taskId);
    }

    /// <summary>Thin seeding helpers so every test states its OWN shape (lifecycle, minutes status/version)
    /// rather than sharing a single canned fixture that would hide which axis each test actually exercises.</summary>
    private sealed class MongoTestContext(
        RecordLinkRepository recordLinks, MeetingRepository meetings, MeetingMinutesVersionRepository minutesVersions)
    {
        public RecordLinkRepository RecordLinks { get; } = recordLinks;

        public async Task<Meeting> SeedMeetingAsync(Guid tenantId, MeetingLifecycle lifecycle)
        {
            var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
            var meeting = new Meeting
            {
                TenantId = tenantId,
                Title = "Review meeting",
                MeetingTypeId = Guid.NewGuid(),
                StartAt = start,
                EndAt = start.AddHours(1),
                OrganizerUserId = TaskTestData.Me,
                Lifecycle = lifecycle,
                IdempotencyKey = Guid.NewGuid().ToString(),
                CreatedBy = "test"
            };
            return await meetings.CreateAsync(meeting);
        }

        public Task LinkAsync(Guid tenantId, Guid taskId, Guid meetingId) => recordLinks.CreateAsync(new RecordLink
        {
            TenantId = tenantId,
            SourceModuleCode = RecordLinkModuleCodes.Meetings,
            SourceRecordId = meetingId,
            TargetModuleCode = RecordLinkModuleCodes.Tasks,
            TargetRecordId = taskId,
            LinkType = RecordLinkTypes.ReviewMeeting,
            CreatedByUserId = TaskTestData.Me,
            CreatedBy = "test"
        });

        public Task SeedMinutesAsync(Guid tenantId, Guid meetingId, int versionNumber, MinutesStatus status)
            => minutesVersions.TryCreateAsync(new MeetingMinutesVersion
            {
                TenantId = tenantId,
                MeetingId = meetingId,
                VersionNumber = versionNumber,
                Status = status,
                CreatedBy = "test"
            });
    }
}
