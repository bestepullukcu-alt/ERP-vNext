using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditDateTimeOffsetRegressionInventoryTests
{
    private static readonly Regex MemberPattern = new(
        @"\bDateTimeOffset\??\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void PersistedDateTimeOffsetInventory_HasNoUnreviewedSurfaceChange()
    {
        var inventory = Inventory();
        string[] reviewedAuditOutboxMembers =
        [
            "Diten.Platform.Infrastructure/Persistence/Models/AuditOutboxMessage.cs:CreatedAtUtc",
            "Diten.Platform.Infrastructure/Persistence/Models/AuditOutboxMessage.cs:NextAttemptAtUtc"
        ];
        var actualReviewedMembers = inventory
            .Where(item => reviewedAuditOutboxMembers.Contains(item, StringComparer.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(reviewedAuditOutboxMembers, actualReviewedMembers);

        // Every member main added after the 413 baseline was frozen is NAMED here, not absorbed into a new number.
        var actualMainAdditions = inventory
            .Where(item => MainAdditionsReviewed20261001.Contains(item, StringComparer.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            MainAdditionsReviewed20261001.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            actualMainAdditions);
        Assert.Equal(
            Bl521AdditionsReviewed20261009,
            inventory.Where(item => Bl521AdditionsReviewed20261009.Contains(item, StringComparer.Ordinal)).ToArray());

        // The current-main remainder is an inventory baseline, not FU02 semantic review or migration scope.
        // The count and fingerprint are the ORIGINAL ones: the named additions above are taken out first, so a
        // 34th member — or a change to any of the 413 — still fails here.
        var currentMainBaseline = inventory
            .Where(item => !reviewedAuditOutboxMembers.Contains(item, StringComparer.Ordinal)
                           && !MainAdditionsReviewed20261001.Contains(item, StringComparer.Ordinal)
                           && !Bl521AdditionsReviewed20261009.Contains(item, StringComparer.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(413, currentMainBaseline.Length);
        Assert.Equal(
            "2096BAAC2D48491C021D155B4F8CF603D1DAF13A8B48A03856561D1CE227BA13",
            Fingerprint(currentMainBaseline));

        Assert.Equal(413 + reviewedAuditOutboxMembers.Length + MainAdditionsReviewed20261001.Length + Bl521AdditionsReviewed20261009.Length, inventory.Length);
        Assert.Equal(449, inventory.Length);
        Assert.Equal(
            "7716A8B5C0FD2588F09F9356A3B07D524228D570F135D574DEBC83070A866E8F",
            Fingerprint(inventory));
    }

    /*
     * THE 33 MEMBERS MAIN ADDED BETWEEN THE FROZEN BASELINE (Codex's branch, e7189ba4d: 413 + 2) AND THE TAKEOVER
     * MERGE (2026-10-01: 446 + 2). Measured per revision, member by member: all 33 exist on the main-side parent
     * (c5d8f8e9f) and none on Codex's side; Codex's branch added none of its own.
     *
     * PERSISTED FORM, the same for every one of them: no [BsonRepresentation]/[BsonSerializer] on the member, no
     * class map, and no global DateTimeOffset serializer (GlobalDateTimeOffsetSerializer_RemainsAbsent) — so each
     * is written by the driver's default as a BSON ARRAY [localTicks (Int64), offsetMinutes (Int32)], the BL-030
     * shape. None is a scalar date, so none is range-sortable on the server. The second test below holds the
     * "no attribute" half; a member that gains one is a storage change and fails there.
     *
     * This list is a review record, not a licence: a new member is added here only after its stored form is written
     * down, exactly as above.
     */
    /// <summary>
    /// Merge WP-CT-LANE-BL521-MERGE-01 (2026-10-09) — the ONE member BL-521 added, named as the 33 above are: FIX4 796c5a592,
    /// PlatformAdministrator.AuthSyncPendingSinceUtc (nullable; set while an Auth sync of the record is outstanding, cleared
    /// when it converges). Measured: the lane (bc1c92e41) has 448 members and B74D9BE7…; the merge adds exactly this one.
    /// </summary>
    private static readonly string[] Bl521AdditionsReviewed20261009 =
    [
        "Diten.Platform.Domain/Entities/PlatformAdministrator.cs:AuthSyncPendingSinceUtc"
    ];

    private static readonly string[] MainAdditionsReviewed20261001 =
    [
        "Diten.Platform.Domain/Entities/DocumentManagement/DocumentRegisterImportBatch.cs:AppliedAt",
        "Diten.Platform.Domain/Entities/EvidenceLinking/EvidenceLink.cs:LinkedAt",
        "Diten.Platform.Domain/Entities/EvidenceLinking/EvidenceLink.cs:RemovedAt",
        "Diten.Platform.Domain/Entities/Meetings/Meeting.cs:EndAt",
        "Diten.Platform.Domain/Entities/Meetings/Meeting.cs:StartAt",
        "Diten.Platform.Domain/Entities/Meetings/MeetingAttendee.cs:MailUndeliveredAt",
        "Diten.Platform.Domain/Entities/Meetings/MeetingMinutesVersion.cs:PublishedAtUtc",
        "Diten.Platform.Domain/Entities/Meetings/MeetingSeries.cs:DeletedAt",
        "Diten.Platform.Domain/Entities/Meetings/MeetingSeries.cs:EndsAt",
        "Diten.Platform.Domain/Entities/Meetings/MeetingSeries.cs:LastGeneratedAt",
        "Diten.Platform.Domain/Entities/Meetings/MeetingSeries.cs:StartsAt",
        "Diten.Platform.Domain/Entities/Meetings/RecordLink.cs:DeletedAt",
        "Diten.Platform.Domain/Entities/Notifications/NotificationDispatch.cs:PermanentlyFailedNotifiedAt",
        "Diten.Platform.Domain/Entities/Organization/OrganizationFieldDefinition.cs:DeletedAt",
        "Diten.Platform.Domain/Entities/Organization/OrganizationFieldValue.cs:DeletedAt",
        "Diten.Platform.Domain/Entities/Tasks/TaskAttachment.cs:DeletedAt",
        "Diten.Platform.Domain/Entities/Tasks/TaskAttachment.cs:UploadedAt",
        "Diten.Platform.Domain/Entities/Tasks/TaskItem.cs:PlannedStartAt",
        "Diten.Platform.Domain/Entities/TimeEntry/TaskTimeTotal.cs:RecomputedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimeEntryNotificationMark.cs:CreatedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimeEntrySettings.cs:ChangedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimeSuggestion.cs:DecidedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimerSegment.cs:AutoCloseNotifiedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimerSegment.cs:MinimisedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimerSegment.cs:StartedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimerSegment.cs:StoppedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimesheetWeek.cs:ApprovedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimesheetWeek.cs:LastRejectedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimesheetWeek.cs:ReopenedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimesheetWeek.cs:SubmittedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimesheetWeek.cs:SupersededAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimesheetWeek.cs:TotalsAppliedAtUtc",
        "Diten.Platform.Domain/Entities/TimeEntry/TimesheetWeek.cs:WithdrawnAtUtc"
    ];

    [Fact]
    public void MainAdditions_CarryNoStorageAttribute_SoTheyKeepTheDefaultArrayForm()
    {
        var src = PlatformSourceRoot();
        var attributed = new List<string>();
        // Merge WP-CT-LANE-BL521-MERGE-01 FIX1 (small 4): the BL-521 member is held to the same rule (CT sabotage M5).
        foreach (var item in MainAdditionsReviewed20261001.Concat(Bl521AdditionsReviewed20261009))
        {
            var separator = item.LastIndexOf(':');
            var lines = File.ReadAllLines(Path.Combine(src, item[..separator].Replace('/', Path.DirectorySeparatorChar)));
            var member = new Regex(@"\bDateTimeOffset\??\s+" + Regex.Escape(item[(separator + 1)..]) + @"\s*\{");
            var index = Array.FindIndex(lines, line => member.IsMatch(line));
            Assert.True(index >= 0, $"{item} is listed but no longer declared.");

            var sameLine = lines[index].Contains("[Bson", StringComparison.Ordinal);
            var above = index > 0 && lines[index - 1].TrimStart().StartsWith('[');
            if (sameLine || above)
            {
                attributed.Add(item);
            }
        }

        Assert.True(attributed.Count == 0,
            "these members now carry an attribute, so their stored form is no longer the reviewed default array — "
            + "re-review and record the new form:\n" + string.Join("\n", attributed));
    }

    [Fact]
    public void GlobalDateTimeOffsetSerializer_RemainsAbsent()
    {
        var source = string.Join(
            "\n",
            SourceFiles().Select(File.ReadAllText));

        Assert.DoesNotMatch(
            new Regex(@"RegisterSerializer\s*<\s*DateTimeOffset\s*>", RegexOptions.CultureInvariant),
            source);
        Assert.DoesNotContain("new DateTimeOffsetSerializer", source, StringComparison.Ordinal);
    }

    private static string[] Inventory()
    {
        var src = PlatformSourceRoot();
        return SourceFiles()
            .SelectMany(file => MemberPattern.Matches(File.ReadAllText(file))
                .Select(match => $"{Path.GetRelativePath(src, file).Replace('\\', '/')}:{match.Groups[1].Value}"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Fingerprint(IEnumerable<string> values) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));

    private static IEnumerable<string> SourceFiles()
    {
        var src = PlatformSourceRoot();
        var roots = new[]
        {
            Path.Combine(src, "Diten.Platform.Domain"),
            Path.Combine(src, "Diten.Platform.Infrastructure")
        };

        return roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static string PlatformSourceRoot() => Path.Combine(
        RepoRoot(),
        "services",
        "Diten.Platform",
        "src");

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
