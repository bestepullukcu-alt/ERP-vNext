using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX5 item 14 — WHEN A COMMIT'S ANSWER NEVER CAME BACK, THE RECORD READ AGAIN DECIDES — AND ANOTHER WRITER'S
/// VERSION + 1 IS NOT OURS. FIX4 answers an unknown commit by reading the record: "written" is only a record that now sits at
/// exactly the version this write produced AND holds what it wrote. A record that sits at the same version because ANOTHER
/// writer produced it (the commit never reached the database, or it was rolled back, and a second request then wrote the same
/// next version) must not count as written — answering 204 for it would tell the caller its change was saved while a stranger's
/// is what is stored.
///
/// <para>The decision is a pure function of the stored record, the version the write started from and what it wrote, so it is
/// measured exactly: one case per way a stranger's record can differ. (Interleaving a second writer between an aborted
/// transaction and the re-read cannot be made deterministic from outside: the transaction holds the document until it ends.
/// The end-to-end "durably written / never sent" pair is in <c>PlatformAdministratorLastSuperAdminRaceMongoTests</c>.)</para>
/// </summary>
public sealed class PlatformAdministratorLandedDecisionTests
{
    private const int StartedFrom = 4;
    private const string Actor = "writing.admin@bl521.test";

    private static PlatformAdministrator Written() => new()
    {
        Email = "landed@bl521.test", NormalizedEmail = "landed@bl521.test", UserName = "landed", NormalizedUserName = "landed",
        DisplayName = "Landed Probe", ActorType = ActorType.PlatformAdmin, PartnerId = null, AllowedTenantIds = [],
        Status = AdministratorStatus.Active, Roles = [AdministratorRole.SupportAdmin], LastStatusReason = "reason", UpdatedBy = Actor,
        Version = StartedFrom + 1
    };

    /// <summary>What the stored record would look like if it were this write, field for field.</summary>
    private static PlatformAdministrator Same() => Written();

    public static TheoryData<string, Action<PlatformAdministrator>> AnotherWritersRecord() => new()
    {
        { "e-mail", r => r.Email = "someone.else@bl521.test" },
        { "user name", r => r.UserName = "someone-else" },
        { "display name", r => r.DisplayName = "Another Writer" },
        { "actor type", r => { r.ActorType = ActorType.PartnerAdmin; } },
        { "partner", r => r.PartnerId = Guid.NewGuid() },
        { "tenant scope", r => r.AllowedTenantIds = [Guid.NewGuid()] },
        { "status", r => r.Status = AdministratorStatus.Suspended },
        { "roles", r => r.Roles = [AdministratorRole.SuperAdmin] },
        { "status reason", r => r.LastStatusReason = "another reason" },
        { "updated by", r => r.UpdatedBy = "another.admin@bl521.test" }
    };

    [Fact]
    public void A_record_holding_exactly_what_the_write_wrote_at_the_next_version_is_ours()
    {
        Assert.True(PlatformAdministratorRepository.UpdateLanded(Same(), StartedFrom, Written()));
    }

    [Theory]
    [MemberData(nameof(AnotherWritersRecord))]
    public void Another_writers_next_version_with_other_content_is_not_ours(string differs, Action<PlatformAdministrator> change)
    {
        var stored = Same();
        change(stored);

        Assert.False(PlatformAdministratorRepository.UpdateLanded(stored, StartedFrom, Written()), $"a record that differs in {differs} was taken for this write");
    }

    [Theory]
    [InlineData(StartedFrom)]       // nothing was written at all
    [InlineData(StartedFrom + 2)]   // somebody wrote after, or instead
    public void A_record_at_any_other_version_is_not_ours(int version)
    {
        var stored = Same();
        stored.Version = version;

        Assert.False(PlatformAdministratorRepository.UpdateLanded(stored, StartedFrom, Written()));
    }

    [Fact]
    public void A_deleted_or_missing_record_is_not_a_written_update()
    {
        var deleted = Same();
        deleted.IsDeleted = true;

        Assert.False(PlatformAdministratorRepository.UpdateLanded(deleted, StartedFrom, Written()));
        Assert.False(PlatformAdministratorRepository.UpdateLanded(null, StartedFrom, Written()));
    }

    // ── the soft delete's decision ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_record_deleted_at_the_next_version_by_this_actor_is_our_delete()
    {
        var stored = Same();
        stored.IsDeleted = true;

        Assert.True(PlatformAdministratorRepository.SoftDeleteLanded(stored, StartedFrom, Actor));
    }

    /// <summary>Another request deleted the record (same next version, another actor): the commit that was lost was not it.</summary>
    [Fact]
    public void A_record_deleted_at_the_next_version_by_another_actor_is_not_our_delete()
    {
        var stored = Same();
        stored.IsDeleted = true;
        stored.UpdatedBy = "another.admin@bl521.test";

        Assert.False(PlatformAdministratorRepository.SoftDeleteLanded(stored, StartedFrom, Actor));
    }

    [Theory]
    [InlineData(StartedFrom, true)]       // deleted, but at the version it started from
    [InlineData(StartedFrom + 2, true)]   // deleted later
    [InlineData(StartedFrom + 1, false)]  // at the next version but not deleted
    public void A_record_not_deleted_at_exactly_the_next_version_is_not_our_delete(int version, bool deleted)
    {
        var stored = Same();
        stored.Version = version;
        stored.IsDeleted = deleted;

        Assert.False(PlatformAdministratorRepository.SoftDeleteLanded(stored, StartedFrom, Actor));
        Assert.False(PlatformAdministratorRepository.SoftDeleteLanded(null, StartedFrom, Actor));
    }
}
