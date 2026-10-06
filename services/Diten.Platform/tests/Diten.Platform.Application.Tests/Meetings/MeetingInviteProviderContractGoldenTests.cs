using System.Text.Json;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Providers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// Go-live, 2026-09-13 — scenario D's contract half. <see cref="TaskProviderContractGoldenTests"/> puts the REAL
/// <c>TaskWorkItemProvider</c> output through the REAL WC-1 validator; nothing did the same for
/// <see cref="MeetingWorkItemProvider"/>. The only meetingInvite the validator ever saw was hand-built in
/// <c>workcenter-next-fixture-contract.test.js</c> — a sample of the contract, not what the provider emits.
///
/// <para>Same golden-file pair: this runs the REAL provider across {Read permission yes, no} × {no linked task,
/// linked task} for one pending invitation, serializes each cell with the runtime's JSON options, and compares it
/// to <c>frontend/Diten.Web/tests/fixtures/meeting-invite-provider-projection.json</c>, which
/// <c>tests/meeting-invite-provider-contract-conformance.test.js</c> runs through the REAL
/// <c>validateWorkItem</c>. The meeting starts in 2099 so the provider's own "today or later" filter and the SLA
/// state stay the same on every run.</para>
/// </summary>
public sealed class MeetingInviteProviderContractGoldenTests
{
    private const string RegenerateEnvVar = "REGENERATE_WCN_FIXTURE";
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly Guid MeetingId = Guid.Parse("e1e1e1e1-0000-0000-0000-0000000000e1");
    private static readonly Guid TypeId = Guid.Parse("e2e2e2e2-0000-0000-0000-0000000000e2");
    private static readonly Guid OrganizerId = Guid.Parse("e3e3e3e3-0000-0000-0000-0000000000e3");
    private static readonly Guid InviteeId = Guid.Parse("e4e4e4e4-0000-0000-0000-0000000000e4");
    private static readonly Guid AttendeeRowId = Guid.Parse("e5e5e5e5-0000-0000-0000-0000000000e5");
    private static readonly Guid TaskId = Guid.Parse("e6e6e6e6-0000-0000-0000-0000000000e6");
    private static readonly DateTimeOffset StartAt = new(2099, 1, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] Permissions = ["read", "none"];
    private static readonly string[] Links = ["none", "linkedTask"];

    [Fact]
    public async Task Every_cell_of_the_invite_matrix_matches_the_golden_fixture()
    {
        var actual = new SortedDictionary<string, WorkItemProjectionDto?>(StringComparer.Ordinal);
        foreach (var permission in Permissions)
        foreach (var link in Links)
        {
            actual[$"invitee_{permission}_{link}"] = await ProjectCellAsync(permission == "read", link == "linkedTask");
        }

        // A null cell would give the JS side nothing to validate; every cell here is a pending invite and must exist.
        Assert.All(actual, pair => Assert.True(pair.Value is not null, $"{pair.Key}: the provider emitted no item"));

        var path = FixturePath();
        if (Environment.GetEnvironmentVariable(RegenerateEnvVar) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(actual, WireOptions));
            return;
        }

        Assert.True(
            File.Exists(path),
            $"No golden fixture at '{path}'. Generate it with:\n{RegenerateEnvVar}=1 dotnet test " +
            "services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~MeetingInviteProviderContractGoldenTests");

        var golden = JsonSerializer.Deserialize<SortedDictionary<string, JsonElement>>(File.ReadAllText(path), WireOptions)!;
        var mismatched = actual.Keys.Union(golden.Keys)
            .Where(key => !golden.TryGetValue(key, out var expected)
                || !actual.TryGetValue(key, out var got)
                || JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(
                    JsonDocument.Parse(JsonSerializer.Serialize(got, WireOptions)).RootElement))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            mismatched.Count == 0,
            "Golden fixture is stale for cell(s): " + string.Join(", ", mismatched) + ". Regenerate with:\n" +
            $"{RegenerateEnvVar}=1 dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests " +
            "--filter FullyQualifiedName~MeetingInviteProviderContractGoldenTests");
    }

    private static async Task<WorkItemProjectionDto?> ProjectCellAsync(bool hasRead, bool linkedTask)
    {
        var meetings = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        meetings.Seed(new Meeting
        {
            Id = MeetingId, TenantId = TaskTestData.Tenant, Title = "Q4 Management Review", MeetingTypeId = TypeId,
            StartAt = StartAt, EndAt = StartAt.AddHours(1), Location = "Room 2", OrganizerUserId = OrganizerId,
            IdempotencyKey = "golden", CreatedBy = "test"
        });

        var types = new FakeMeetingTypeRepository { Tenant = TaskTestData.Tenant };
        types.Seed(new MeetingType { Id = TypeId, TenantId = TaskTestData.Tenant, Name = "MGMT-REVIEW", CreatedBy = "test" });

        var attendees = new FakeMeetingAttendeeRepository { Tenant = TaskTestData.Tenant };
        attendees.Seed(new MeetingAttendee
        {
            Id = AttendeeRowId, TenantId = TaskTestData.Tenant, MeetingId = MeetingId, UserId = OrganizerId,
            InvitationResponse = InvitationResponse.Accepted, CreatedBy = "test"
        });
        attendees.Seed(new MeetingAttendee
        {
            TenantId = TaskTestData.Tenant, MeetingId = MeetingId, UserId = InviteeId,
            InvitationResponse = InvitationResponse.Pending, CreatedBy = "test"
        });

        RecordLinkService? recordLinks = null;
        FakeRelatedRecordResolverRegistry? resolvers = null;
        if (linkedTask)
        {
            recordLinks = new RecordLinkService(
                new FakeRecordLinkRepository(new RecordLink
                {
                    TenantId = TaskTestData.Tenant,
                    SourceModuleCode = RecordLinkModuleCodes.Meetings,
                    SourceRecordId = MeetingId,
                    TargetModuleCode = RecordLinkModuleCodes.Tasks,
                    TargetRecordId = TaskId,
                    LinkType = RecordLinkTypes.BornFromMeeting,
                    CreatedByUserId = OrganizerId,
                    CreatedBy = "test"
                }),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(InviteeId));
            resolvers = new FakeRelatedRecordResolverRegistry(new FakeRelatedRecordResolver(
                RecordLinkModuleCodes.Tasks,
                new Dictionary<Guid, RelatedRecordSummary> { [TaskId] = new("Prepare the KPI pack", $"/Tasks/{TaskId}") }));
        }

        var provider = new MeetingWorkItemProvider(
            attendees, meetings, types, new FakeUserDisplayNameResolver((OrganizerId, "Ayşe Yılmaz")), SlaForTests.Real(),
            recordLinks, resolvers);

        var actor = new WorkItemActor(
            InviteeId, IsPlatformActor: false,
            new HashSet<string>(hasRead ? [MeetingPermissions.Read] : [], StringComparer.Ordinal));

        var items = await provider.GetWorkItemsAsync(actor, CancellationToken.None);
        return items.FirstOrDefault(item => item.Id == MeetingId.ToString());
    }

    /// <summary>The checkout this test runs in — the nearest directory holding a ".git" entry (a file in a linked
    /// worktree), the same rule <see cref="TaskProviderContractGoldenTests"/> uses (BL-383).</summary>
    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, ".git"))
               && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "frontend", "Diten.Web", "tests", "fixtures", "meeting-invite-provider-projection.json");
    }
}
