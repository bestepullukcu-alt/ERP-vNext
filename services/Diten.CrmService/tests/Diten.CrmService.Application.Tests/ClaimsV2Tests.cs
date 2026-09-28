using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Diten.CrmService.Application.Common.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-CL-BE-1 (claims v2) — core / local claims, country versions, claim × country closures, core new version and its
/// review-required propagation, the coverage matrix, class maps, tenant isolation and PII-safe audit. In-memory fakes
/// (handlers mutate the tracked reference in place, Update is a no-op). Reference sets are faked the way MOD-0048
/// publishes them — nothing here is a compiled country or language list in production code.
/// </summary>
public sealed class ClaimsV2Tests
{
    private static readonly Guid TenantA = Guid.Parse("a1a1a1a1-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("b2b2b2b2-0000-0000-0000-00000000000b");
    private static readonly Guid Product = Guid.Parse("0000aaaa-0000-0000-0000-000000000001");
    private static readonly Guid Aud1 = Guid.Parse("0000bbbb-0000-0000-0000-000000000001");
    private static readonly Guid Aud2 = Guid.Parse("0000bbbb-0000-0000-0000-000000000002");
    private static readonly Guid Aud3 = Guid.Parse("0000bbbb-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] BrdCountries = { "TR", "BY", "UZ", "TM", "GE", "AZ" };

    // ============================================================ fixture

    private sealed class Fixture
    {
        public FakeClaimRepo Claims { get; }
        public FakeCountryVersionRepo Versions { get; }
        public FakeReferences Refs { get; }
        public FakeProducts Products { get; }
        public FakeAudiences Audiences { get; }
        public CapturingAudit Audit { get; } = new();
        public Guid TenantId { get; }
        public FakeSettings Settings { get; } = new();

        public Fixture(Guid tenant, Fixture? shareStoresWith = null)
        {
            TenantId = tenant;
            Claims = shareStoresWith?.Claims ?? new FakeClaimRepo();
            Versions = shareStoresWith?.Versions ?? new FakeCountryVersionRepo();
            Refs = shareStoresWith?.Refs ?? FakeReferences.Default();
            Products = shareStoresWith?.Products ?? new FakeProducts();
            Audiences = shareStoresWith?.Audiences ?? new FakeAudiences(TenantA, Aud1, Aud2, Aud3);
        }

        private TenantContext Ctx()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantId);
            return ctx;
        }

        private static NullActorContext Actor() => new();

        public CreateClaimHandler Create()
            => new(Ctx(), Actor(), Claims, Audit, Refs, Refs, Products, Audiences);
        public UpdateClaimHandler Update() => new(Ctx(), Actor(), Claims, Audit, Refs, Products, Audiences);
        public ApproveClaimHandler Approve() => new(Ctx(), Actor(), Claims, Audit, Versions);
        public GetClaimHandler Get() => new(Ctx(), Claims, Versions);
        public ListClaimsHandler List() => new(Ctx(), Claims, Versions);
        public CreateClaimNewVersionHandler NewVersion() => new(Ctx(), Actor(), Claims, Audit);
        public CloseClaimCountryHandler Close() => new(Ctx(), Actor(), Claims, Versions, Refs, Audit);
        public ReopenClaimCountryHandler Reopen() => new(Ctx(), Actor(), Claims, Audit);
        public CreateClaimCountryVersionHandler CreateVersion()
            => new(Ctx(), Actor(), Claims, Versions, Refs, Refs, Audiences, Audit);
        public UpdateClaimCountryVersionHandler UpdateVersion()
            => new(Ctx(), Actor(), Claims, Versions, Refs, Refs, Audiences, Audit);
        public CreateClaimCountryNewVersionHandler NewCountryVersion() => new(Ctx(), Actor(), Claims, Versions, Audit);
        public ApproveClaimCountryVersionHandler ApproveVersion() => new(Ctx(), Actor(), Claims, Versions, Refs, Audit);
        public ArchiveClaimCountryVersionHandler ArchiveVersion() => new(Ctx(), Actor(), Versions, Audit);
        public GetClaimCountryVersionHandler GetVersion() => new(Ctx(), Versions, Settings);
        public ListClaimCountryVersionsHandler ListVersions() => new(Ctx(), Claims, Versions, Settings);
        public GetClaimCoverageHandler Coverage() => new(Ctx(), Claims, Versions, Refs, Settings);

        public async Task<Guid> SeedCore(string code, bool approve = true, IReadOnlyList<Guid>? audiences = null,
            string text = "Reduces symptoms")
        {
            var r = await Create().Handle(new CreateClaimCommand(code, "Claim " + code, text, Jan1,
                Kind: ClaimKinds.Core, ProductId: Product, ProductDisplay: "PRD-1", AudienceProfileIds: audiences),
                default);
            Assert.Equal(201, r.StatusCode);
            if (approve)
            {
                Assert.True((await Approve().Handle(new ApproveClaimCommand(r.Data), default)).IsSuccessful);
            }

            return r.Data;
        }

        public async Task<Guid> SeedLocal(string code, string country)
        {
            var r = await Create().Handle(new CreateClaimCommand(code, "Local " + code, "Yerel metin", Jan1,
                Kind: ClaimKinds.Local, LocalCountryCode: country, ProductId: Product), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public Task<Response<Guid>> OpenVersion(Guid claimId, string country,
            IReadOnlyList<ClaimLocalizedTextInput>? texts = null, string adaptation = "verbatim",
            string? reason = null, IReadOnlyList<Guid>? audiences = null, DateTimeOffset? validTo = null)
            => CreateVersion().Handle(new CreateClaimCountryVersionCommand(
                claimId, country, texts ?? Texts(country), null, adaptation, reason, audiences,
                DateTimeOffset.UtcNow.Date, validTo), default);

        public async Task<Guid> OpenAndApprove(Guid claimId, string country, DateTimeOffset? validTo = null)
        {
            var r = await OpenVersion(claimId, country, validTo: validTo);
            Assert.Equal(201, r.StatusCode);
            Assert.True((await ApproveVersion().Handle(new ApproveClaimCountryVersionCommand(r.Data), default))
                .IsSuccessful);
            return r.Data;
        }
    }

    /// <summary>A text for every content language of the (faked) country.</summary>
    private static IReadOnlyList<ClaimLocalizedTextInput> Texts(string country)
        => FakeReferences.LanguagesOf(country).Select(l => new ClaimLocalizedTextInput(l, $"text-{l}")).ToList();

    private static string Code(Response<Guid> r) => r.Errors![0];
    private static string Code(Response<bool> r) => r.Errors![0];

    // ============================================================ core / local claim

    [Fact]
    public async Task Pre_v2_create_without_kind_still_works_and_defaults_to_core_en()
    {
        var fx = new Fixture(TenantA);
        var r = await fx.Create().Handle(new CreateClaimCommand("CL-OLD", "Old", "Old text", Jan1), default);
        Assert.Equal(201, r.StatusCode);

        var dto = (await fx.Get().Handle(new GetClaimQuery(r.Data), default)).Data!;
        Assert.Equal(ClaimKinds.Core, dto.Kind);
        Assert.Equal("en", dto.TextLanguageCode);
        Assert.Null(dto.ProductId);
        Assert.Empty(dto.CountrySummary!);
    }

    [Fact]
    public async Task Kind_given_requires_product_and_product_is_proven_fail_closed()
    {
        var fx = new Fixture(TenantA);
        var noProduct = await fx.Create().Handle(
            new CreateClaimCommand("CL-NP", "n", "t", Jan1, Kind: ClaimKinds.Core), default);
        Assert.Equal(ClaimErrorCodes.ProductRequired, Code(noProduct));

        var unknown = await fx.Create().Handle(new CreateClaimCommand("CL-UP", "n", "t", Jan1,
            Kind: ClaimKinds.Core, ProductId: Guid.NewGuid()), default);
        Assert.Equal(400, unknown.StatusCode);
        Assert.Equal(ClaimErrorCodes.ProductNotFound, Code(unknown));

        fx.Products.Unavailable = true;
        var down = await fx.Create().Handle(new CreateClaimCommand("CL-DN", "n", "t", Jan1,
            Kind: ClaimKinds.Core, ProductId: Product), default);
        Assert.Equal(503, down.StatusCode);
        Assert.Empty(fx.Claims.Items); // nothing persisted
    }

    [Fact]
    public async Task Unknown_audience_profile_is_rejected()
    {
        var fx = new Fixture(TenantA);
        var r = await fx.Create().Handle(new CreateClaimCommand("CL-AU", "n", "t", Jan1, Kind: ClaimKinds.Core,
            ProductId: Product, AudienceProfileIds: new[] { Guid.NewGuid() }), default);
        Assert.Equal(ClaimErrorCodes.AudienceNotFound, Code(r));
    }

    [Fact]
    public async Task Local_claim_needs_a_brd_country_and_a_country_language()
    {
        var fx = new Fixture(TenantA);
        var noCountry = await fx.Create().Handle(new CreateClaimCommand("CL-L0", "n", "t", Jan1,
            Kind: ClaimKinds.Local, ProductId: Product), default);
        Assert.Equal(ClaimErrorCodes.LocalCountryRequired, Code(noCountry));

        var offList = await fx.Create().Handle(new CreateClaimCommand("CL-L1", "n", "t", Jan1,
            Kind: ClaimKinds.Local, LocalCountryCode: "KZ", ProductId: Product), default);
        Assert.Equal(ClaimErrorCodes.InvalidReferenceValue, Code(offList));

        var badLanguage = await fx.Create().Handle(new CreateClaimCommand("CL-L2", "n", "t", Jan1,
            Kind: ClaimKinds.Local, LocalCountryCode: "UZ", ProductId: Product, TextLanguageCode: "en"), default);
        Assert.Equal(ClaimErrorCodes.LanguageNotAllowed, Code(badLanguage));

        var id = await fx.SeedLocal("CL-L3", "uz");
        var dto = (await fx.Get().Handle(new GetClaimQuery(id), default)).Data!;
        Assert.Equal("UZ", dto.LocalCountryCode);
        Assert.Equal("uz", dto.TextLanguageCode); // first BRD language of UZ
    }

    [Fact]
    public async Task Local_claim_only_its_own_country_other_cells_not_applicable()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedLocal("CL-LOC", "UZ");

        // No core approval is needed for a local claim — but only its own country.
        var other = await fx.OpenVersion(id, "TR");
        Assert.Equal(ClaimErrorCodes.NotApplicable, Code(other));
        var close = await fx.Close().Handle(new CloseClaimCountryCommand(id, "TR", "no-license"), default);
        Assert.Equal(ClaimErrorCodes.NotApplicable, Code(close));

        var own = await fx.OpenVersion(id, "UZ");
        Assert.Equal(201, own.StatusCode);

        var row = (await fx.Coverage().Handle(new GetClaimCoverageQuery(), default)).Data!.Rows.Single();
        Assert.Equal(ClaimCoverageStates.Draft, row.Cells.Single(c => c.CountryCode == "UZ").State);
        Assert.All(row.Cells.Where(c => c.CountryCode != "UZ"),
            c => Assert.Equal(ClaimCoverageStates.NotApplicable, c.State));
    }

    // ============================================================ country version rules

    [Fact]
    public async Task Core_not_approved_blocks_country_version()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-NA", approve: false);
        var r = await fx.OpenVersion(id, "TR");
        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ClaimErrorCodes.CoreNotApproved, Code(r));
        Assert.Empty(fx.Versions.Items);
    }

    [Fact]
    public async Task Closed_cell_blocks_version_and_reopen_allows_it()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-CC");
        Assert.True((await fx.Close().Handle(new CloseClaimCountryCommand(id, "tr", "no-license"), default))
            .IsSuccessful);

        var blocked = await fx.OpenVersion(id, "TR");
        Assert.Equal(409, blocked.StatusCode);
        Assert.Equal(ClaimErrorCodes.CountryClosed, Code(blocked));

        var again = await fx.Close().Handle(new CloseClaimCountryCommand(id, "TR", "business-decision"), default);
        Assert.Equal(ClaimErrorCodes.CountryAlreadyClosed, Code(again));

        Assert.True((await fx.Reopen().Handle(new ReopenClaimCountryCommand(id, "TR", "licence granted"), default))
            .IsSuccessful);
        Assert.Equal(201, (await fx.OpenVersion(id, "TR")).StatusCode);

        // Append-only history: the closure entry is stamped, not removed.
        var closure = fx.Claims.Items.Single(c => c.Id == id).CountryClosures.Single();
        Assert.Equal("TR", closure.CountryCode);
        Assert.Equal("no-license", closure.ReasonCode);
        Assert.NotNull(closure.ReopenedAt);
        Assert.Equal("licence granted", closure.ReopenNote);

        var notClosed = await fx.Reopen().Handle(new ReopenClaimCountryCommand(id, "TR"), default);
        Assert.Equal(ClaimErrorCodes.CountryNotClosed, Code(notClosed));
    }

    [Fact]
    public async Task Closing_validates_country_and_reason_against_brd()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-CR");
        Assert.Equal(ClaimErrorCodes.InvalidReferenceValue,
            Code(await fx.Close().Handle(new CloseClaimCountryCommand(id, "KZ", "no-license"), default)));
        Assert.Equal(ClaimErrorCodes.InvalidReferenceValue,
            Code(await fx.Close().Handle(new CloseClaimCountryCommand(id, "TR", "whatever"), default)));

        fx.Refs.Sets.Remove(ClaimReferenceSets.ClosureReason);
        Assert.Equal(ClaimErrorCodes.ReferenceSetMissing,
            Code(await fx.Close().Handle(new CloseClaimCountryCommand(id, "TR", "no-license"), default)));
    }

    [Fact]
    public async Task Closing_a_country_with_a_version_returns_country_has_version()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-HV");
        var v = await fx.OpenVersion(id, "GE");
        Assert.Equal(201, v.StatusCode);

        var r = await fx.Close().Handle(new CloseClaimCountryCommand(id, "GE", "regulation-disallows"), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ClaimErrorCodes.CountryHasVersion, Code(r));

        // Archived versions do not block.
        await fx.ArchiveVersion().Handle(new ArchiveClaimCountryVersionCommand(v.Data), default);
        Assert.True((await fx.Close().Handle(
            new CloseClaimCountryCommand(id, "GE", "regulation-disallows"), default)).IsSuccessful);
    }

    [Fact]
    public async Task Second_live_version_for_the_same_country_is_rejected()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-DUPV");
        Assert.Equal(201, (await fx.OpenVersion(id, "AZ")).StatusCode);
        var dup = await fx.OpenVersion(id, "AZ");
        Assert.Equal(ClaimErrorCodes.CountryVersionExists, Code(dup));
    }

    [Fact]
    public async Task Languages_must_be_country_languages_and_missing_set_is_reference_set_missing()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-LG");

        var wrong = await fx.OpenVersion(id, "UZ", new[] { new ClaimLocalizedTextInput("tr", "metin") });
        Assert.Equal(ClaimErrorCodes.LanguageNotAllowed, Code(wrong));

        // Draft: one of the country's languages is enough.
        Assert.Equal(201, (await fx.OpenVersion(id, "UZ", new[] { new ClaimLocalizedTextInput("ru", "текст") }))
            .StatusCode);

        fx.Refs.Sets.Remove(ClaimReferenceSets.CountryContentLanguages);
        var missing = await fx.OpenVersion(id, "TR");
        Assert.Equal(400, missing.StatusCode);
        Assert.Equal(ClaimErrorCodes.ReferenceSetMissing, Code(missing));
    }

    [Fact]
    public async Task Country_without_languages_attribute_is_reference_set_missing()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-NOATTR");
        fx.Refs.Sets[ClaimReferenceSets.CountryContentLanguages].RemoveAll(v => v.Code == "TM");
        fx.Refs.Sets[ClaimReferenceSets.CountryContentLanguages].Add(("TM", new Dictionary<string, string>()));
        var r = await fx.OpenVersion(id, "TM", new[] { new ClaimLocalizedTextInput("tk", "tekst") });
        Assert.Equal(ClaimErrorCodes.ReferenceSetMissing, Code(r));
    }

    [Fact]
    public async Task Adaptation_other_than_verbatim_needs_a_reason_and_code_must_be_in_set()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-AD");

        var noReason = await fx.OpenVersion(id, "TR", adaptation: "narrowed");
        Assert.Equal(ClaimErrorCodes.AdaptationReasonRequired, Code(noReason));

        var badCode = await fx.OpenVersion(id, "TR", adaptation: "rewritten", reason: "x");
        Assert.Equal(ClaimErrorCodes.InvalidReferenceValue, Code(badCode));

        Assert.Equal(201, (await fx.OpenVersion(id, "TR", adaptation: "softened", reason: "local regulator"))
            .StatusCode);
        Assert.Equal(201, (await fx.OpenVersion(id, "BY", adaptation: "verbatim")).StatusCode);
    }

    [Fact]
    public async Task Audience_can_only_narrow_the_core_audience()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-AN", audiences: new[] { Aud1, Aud2 });

        var widen = await fx.OpenVersion(id, "TR", audiences: new[] { Aud1, Aud3 });
        Assert.Equal(400, widen.StatusCode);
        Assert.Equal(ClaimErrorCodes.AudienceNotNarrowing, Code(widen));

        Assert.Equal(201, (await fx.OpenVersion(id, "TR", audiences: new[] { Aud2 })).StatusCode);

        // A core without audiences leaves the country free.
        var free = await fx.SeedCore("CL-AF");
        Assert.Equal(201, (await fx.OpenVersion(free, "TR", audiences: new[] { Aud3 })).StatusCode);
    }

    [Fact]
    public async Task Validity_end_before_start_is_rejected()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-VT");
        var r = await fx.CreateVersion().Handle(new CreateClaimCountryVersionCommand(id, "TR", Texts("TR"), null,
            "verbatim", null, null, Jan1.AddDays(10), Jan1), default);
        Assert.Equal(ClaimErrorCodes.InvalidValidity, Code(r));
    }

    // ============================================================ approval, lock, new versions

    [Fact]
    public async Task Temporary_approve_needs_every_country_language_and_inactivates_previous()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-TA");

        var partial = await fx.OpenVersion(id, "UZ", new[] { new ClaimLocalizedTextInput("uz", "matn") });
        var incomplete = await fx.ApproveVersion().Handle(new ApproveClaimCountryVersionCommand(partial.Data), default);
        Assert.Equal(ClaimErrorCodes.LanguagesIncomplete, Code(incomplete));

        await fx.UpdateVersion().Handle(new UpdateClaimCountryVersionCommand(partial.Data, Texts("UZ"), null,
            "verbatim", null, null, DateTimeOffset.UtcNow.Date), default);
        Assert.True((await fx.ApproveVersion().Handle(new ApproveClaimCountryVersionCommand(partial.Data), default))
            .IsSuccessful);
        var first = fx.Versions.Items.Single(v => v.Id == partial.Data);
        Assert.Equal(ClaimStatuses.Approved, first.Status);

        var next = await fx.NewCountryVersion().Handle(new CreateClaimCountryNewVersionCommand(first.Id), default);
        Assert.True((await fx.ApproveVersion().Handle(new ApproveClaimCountryVersionCommand(next.Data), default))
            .IsSuccessful);
        Assert.Equal(ClaimStatuses.Inactive, first.Status);
        Assert.Equal(ClaimStatuses.Approved, fx.Versions.Items.Single(v => v.Id == next.Data).Status);
    }

    [Fact]
    public async Task Approved_country_version_is_locked_and_new_version_is_minor_plus_one_bound_to_current_core()
    {
        var fx = new Fixture(TenantA);
        var core1 = await fx.SeedCore("CL-NV");
        var v1 = await fx.OpenAndApprove(core1, "TR");

        var locked = await fx.UpdateVersion().Handle(new UpdateClaimCountryVersionCommand(v1, Texts("TR"), null,
            "verbatim", null, null, DateTimeOffset.UtcNow.Date), default);
        Assert.Equal(409, locked.StatusCode);
        Assert.Equal(ClaimErrorCodes.VersionLocked, Code(locked));

        // Core 2.0 approved → TR 1.0 review-required → new country version binds to 2.0.
        var core2 = (await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(core1), default)).Data;
        await fx.Approve().Handle(new ApproveClaimCommand(core2), default);

        var next = await fx.NewCountryVersion().Handle(new CreateClaimCountryNewVersionCommand(v1), default);
        Assert.Equal(201, next.StatusCode);
        var dto = (await fx.GetVersion().Handle(new GetClaimCountryVersionQuery(next.Data), default)).Data!;
        Assert.Equal("1.1", dto.Version);
        Assert.Equal("2.0", dto.BoundCoreVersion);
        Assert.Equal(core2, dto.ClaimId);
        Assert.Equal(ClaimStatuses.Draft, dto.Status);
        Assert.Equal(v1, dto.SupersedesVersionId);
        Assert.Equal(0, dto.ReviewRoundCount); // ReviewRounds stay empty until WP-CL-BE-4

        var secondDraft = await fx.NewCountryVersion().Handle(new CreateClaimCountryNewVersionCommand(v1), default);
        Assert.Equal(ClaimErrorCodes.OpenVersionExists, Code(secondDraft));
    }

    [Fact]
    public async Task Core_new_version_is_major_plus_one_and_approval_propagates()
    {
        var fx = new Fixture(TenantA);
        var core1 = await fx.SeedCore("CL-MJ");
        var tr = await fx.OpenAndApprove(core1, "TR");
        var trApprovedAt = fx.Versions.Items.Single(v => v.Id == tr).ApprovedAt;

        var draftOnly = await fx.SeedCore("CL-MJ-D", approve: false);
        Assert.Equal(ClaimErrorCodes.InvalidState,
            Code(await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(draftOnly), default)));

        var nv = await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(core1), default);
        Assert.Equal(201, nv.StatusCode);
        var core2 = fx.Claims.Items.Single(c => c.Id == nv.Data);
        Assert.Equal("2.0", core2.ClaimVersion);
        Assert.Equal(ClaimStatuses.Draft, core2.Status);
        Assert.Equal(core1, core2.SupersedesClaimId);
        Assert.Equal(Product, core2.ProductId);

        var twice = await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(core1), default);
        Assert.Equal(ClaimErrorCodes.OpenVersionExists, Code(twice));

        // Until the new core is approved nothing moves.
        Assert.Equal(ClaimStatuses.Approved, fx.Versions.Items.Single(v => v.Id == tr).Status);

        await fx.Approve().Handle(new ApproveClaimCommand(core2.Id), default);
        Assert.Equal(ClaimStatuses.Inactive, fx.Claims.Items.Single(c => c.Id == core1).Status);
        var trAfter = fx.Versions.Items.Single(v => v.Id == tr);
        Assert.Equal(ClaimStatuses.ReviewRequired, trAfter.Status);
        Assert.Equal(trApprovedAt, trAfter.ApprovedAt); // kept
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.CountryVersionsReviewRequired);
    }

    [Fact]
    public async Task Legacy_unparseable_claim_version_new_version_assumes_2_0()
    {
        var fx = new Fixture(TenantA);
        var r = await fx.Create().Handle(new CreateClaimCommand("CL-LEG", "Legacy", "t", Jan1,
            ClaimVersion: "final-rev"), default);
        await fx.Approve().Handle(new ApproveClaimCommand(r.Data), default);

        var nv = await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(r.Data), default);
        Assert.Equal("2.0", fx.Claims.Items.Single(c => c.Id == nv.Data).ClaimVersion);
    }

    [Fact]
    public async Task Closures_travel_with_the_claim_code_across_versions()
    {
        var fx = new Fixture(TenantA);
        var core1 = await fx.SeedCore("CL-CT");
        await fx.Close().Handle(new CloseClaimCountryCommand(core1, "TM", "business-decision"), default);

        var core2 = (await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(core1), default)).Data;
        Assert.NotNull(fx.Claims.Items.Single(c => c.Id == core2).ActiveClosureFor("TM"));

        // A closure made while the draft is open reaches both live records.
        await fx.Close().Handle(new CloseClaimCountryCommand(core1, "GE", "no-license"), default);
        Assert.All(fx.Claims.Items.Where(c => c.ClaimCode == "CL-CT"),
            c => Assert.NotNull(c.ActiveClosureFor("GE")));
    }

    [Fact]
    public async Task Approved_core_v2_fields_are_frozen_on_update()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-FZ", audiences: new[] { Aud1 });
        var r = await fx.Update().Handle(new UpdateClaimCommand(id, "Claim CL-FZ", "Reduces symptoms", Jan1,
            AudienceProfileIds: new[] { Aud1, Aud2 }), default);
        Assert.Equal(409, r.StatusCode);

        // A pre-v2 update body (no v2 members) keeps them.
        var legacy = await fx.Update().Handle(new UpdateClaimCommand(id, "Renamed", "Reduces symptoms", Jan1), default);
        Assert.True(legacy.IsSuccessful);
        var entity = fx.Claims.Items.Single(c => c.Id == id);
        Assert.Equal(new[] { Aud1 }, entity.AudienceProfileIds);
        Assert.Equal(Product, entity.ProductId);
    }

    // ============================================================ coverage matrix

    [Fact]
    public async Task Coverage_columns_come_from_brd_and_cells_carry_state()
    {
        var fx = new Fixture(TenantA);
        fx.Settings.Days = 60;
        var id = await fx.SeedCore("CL-MX", audiences: new[] { Aud1, Aud2 });

        var tr = await fx.OpenAndApprove(id, "TR", validTo: DateTimeOffset.UtcNow.AddDays(30));   // approved, expiring
        await fx.OpenAndApprove(id, "BY", validTo: DateTimeOffset.UtcNow.AddDays(200));          // approved
        await fx.OpenVersion(id, "UZ", new[] { new ClaimLocalizedTextInput("uz", "matn") });    // draft
        await fx.Close().Handle(new CloseClaimCountryCommand(id, "GE", "no-license"), default); // closed
        var tm = await fx.OpenVersion(id, "TM");
        fx.Versions.Items.Single(v => v.Id == tm.Data).Status = ClaimStatuses.InReview;       // (workflow later)

        var matrix = (await fx.Coverage().Handle(new GetClaimCoverageQuery(), default)).Data!;
        Assert.Equal(BrdCountries, matrix.Countries.Select(c => c.CountryCode).ToArray());
        Assert.Equal(60, matrix.ExpiringWindowDays);

        var row = matrix.Rows.Single();
        Assert.Equal("CL-MX", row.ClaimCode);
        Assert.Equal(ClaimKinds.Core, row.Kind);
        Assert.Equal(2, row.AudienceCount);
        Assert.Equal("1.0", row.CoreVersion);
        Assert.Equal(ClaimStatuses.Approved, row.CoreStatus);

        var cells = row.Cells.ToDictionary(c => c.CountryCode);
        Assert.Equal(ClaimCoverageStates.Approved, cells["TR"].State);
        Assert.True(cells["TR"].IsExpiring);
        Assert.Equal(tr, cells["TR"].VersionId);
        Assert.Equal("1.0", cells["TR"].Version);
        Assert.Equal("1.0", cells["TR"].BoundCoreVersion);
        Assert.Equal(ClaimCoverageStates.Approved, cells["BY"].State);
        Assert.False(cells["BY"].IsExpiring);
        Assert.Equal(ClaimCoverageStates.Draft, cells["UZ"].State);
        Assert.Equal(ClaimCoverageStates.InReview, cells["TM"].State);
        Assert.Equal(ClaimCoverageStates.Closed, cells["GE"].State);
        Assert.Equal("no-license", cells["GE"].ClosureReasonCode);
        Assert.Equal(ClaimCoverageStates.NotOpened, cells["AZ"].State);

        // Core 2.0 approved → approved country cells become review-required.
        var core2 = (await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(id), default)).Data;
        await fx.Approve().Handle(new ApproveClaimCommand(core2), default);
        var after = (await fx.Coverage().Handle(new GetClaimCoverageQuery(), default)).Data!.Rows.Single();
        Assert.Equal("2.0", after.CoreVersion);
        Assert.Equal(ClaimCoverageStates.ReviewRequired, after.Cells.Single(c => c.CountryCode == "TR").State);

        // The list DTO carries the additive v2 members + a country summary.
        var listed = (await fx.List().Handle(new ListClaimsQuery(), default)).Data!.Items
            .Single(c => c.ClaimId == core2);
        Assert.Equal(Product, listed.ProductId);
        Assert.Contains(listed.CountrySummary!, s => s is { CountryCode: "GE", State: ClaimCoverageStates.Closed });
        Assert.Contains(listed.CountrySummary!,
            s => s is { CountryCode: "TR", State: ClaimCoverageStates.ReviewRequired });
    }

    [Fact]
    public async Task Coverage_axis_follows_brd_and_missing_set_fails_closed()
    {
        var fx = new Fixture(TenantA);
        await fx.SeedCore("CL-AX");
        fx.Refs.Sets[ClaimReferenceSets.CountryCodes].Add(("KZ", null));
        var grown = (await fx.Coverage().Handle(new GetClaimCoverageQuery(), default)).Data!;
        Assert.Equal(7, grown.Countries.Count);
        Assert.Equal(7, grown.Rows.Single().Cells.Count);

        fx.Refs.Sets.Remove(ClaimReferenceSets.CountryCodes);
        var r = await fx.Coverage().Handle(new GetClaimCoverageQuery(), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(ClaimErrorCodes.ReferenceSetMissing, r.Errors![0]);
    }

    [Fact]
    public async Task Coverage_filters_by_kind_product_and_search()
    {
        var fx = new Fixture(TenantA);
        await fx.SeedCore("CL-F1");
        await fx.SeedLocal("CL-F2", "AZ");
        var coverage = fx.Coverage();

        Assert.Equal(new[] { "CL-F2" }, (await coverage.Handle(new GetClaimCoverageQuery(Kind: "local"), default))
            .Data!.Rows.Select(r => r.ClaimCode).ToArray());
        Assert.Equal(2, (await coverage.Handle(new GetClaimCoverageQuery(ProductId: Product), default)).Data!.Rows.Count);
        Assert.Empty((await coverage.Handle(new GetClaimCoverageQuery(ProductId: Guid.NewGuid()), default)).Data!.Rows);
        Assert.Single((await coverage.Handle(new GetClaimCoverageQuery(Search: "f1"), default)).Data!.Rows);
    }

    // ============================================================ tenancy, audit, persistence

    [Fact]
    public async Task Tenant_isolation_country_versions_and_coverage()
    {
        var a = new Fixture(TenantA);
        var id = await a.SeedCore("CL-TI");
        var v = await a.OpenAndApprove(id, "TR");

        var b = new Fixture(TenantB, shareStoresWith: a);
        Assert.Equal(404, (await b.GetVersion().Handle(new GetClaimCountryVersionQuery(v), default)).StatusCode);
        Assert.Equal(404, (await b.ListVersions().Handle(new ListClaimCountryVersionsQuery(id), default)).StatusCode);
        Assert.Equal(404, (await b.OpenVersion(id, "BY")).StatusCode);
        Assert.Equal(404, (await b.Close().Handle(new CloseClaimCountryCommand(id, "BY", "no-license"), default))
            .StatusCode);
        Assert.Equal(404, (await b.ApproveVersion().Handle(new ApproveClaimCountryVersionCommand(v), default))
            .StatusCode);
        Assert.Empty((await b.Coverage().Handle(new GetClaimCoverageQuery(), default)).Data!.Rows);
        Assert.Single((await a.ListVersions().Handle(new ListClaimCountryVersionsQuery(id), default)).Data!);
    }

    [Fact]
    public async Task Audit_events_carry_ids_codes_and_countries_but_never_wording()
    {
        const string Secret = "SECRET-WORDING-7731";
        var fx = new Fixture(TenantA);
        var id = await fx.SeedCore("CL-PII", text: Secret);
        var v = await fx.OpenVersion(id, "TR", new[] { new ClaimLocalizedTextInput("tr", Secret) });
        await fx.ApproveVersion().Handle(new ApproveClaimCountryVersionCommand(v.Data), default);
        await fx.Close().Handle(new CloseClaimCountryCommand(id, "AZ", "no-license"), default);
        await fx.Reopen().Handle(new ReopenClaimCountryCommand(id, "AZ", Secret), default);
        var nv = await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(id), default);
        await fx.Approve().Handle(new ApproveClaimCommand(nv.Data), default);
        await fx.NewCountryVersion().Handle(new CreateClaimCountryNewVersionCommand(v.Data), default);

        var events = fx.Audit.Events.Select(e => e.Event).ToHashSet();
        Assert.Contains(ClaimReasonCodes.CountryVersionCreated, events);
        Assert.Contains(ClaimReasonCodes.CountryVersionApproved, events);
        Assert.Contains(ClaimReasonCodes.CountryClosed, events);
        Assert.Contains(ClaimReasonCodes.CountryReopened, events);
        Assert.Contains(ClaimReasonCodes.NewVersionCreated, events);
        Assert.Contains(ClaimReasonCodes.CountryVersionsReviewRequired, events);
        Assert.All(fx.Audit.Events, e => Assert.DoesNotContain(Secret, e.Detail ?? string.Empty));
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.CountryVersionCreated
            && e.EntityType == ContentCompositionAuditEntities.ClaimCountryVersion
            && e.Detail == "CL-PII|TR|1.0");
    }

    [Fact]
    public void Class_maps_store_guids_as_strings_so_filters_match()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(ClaimCountryVersion)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(ClaimLocalizedText)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(ClaimReviewRound)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(ClaimCountryClosure)));

        var claimId = Guid.NewGuid();
        var version = new ClaimCountryVersion
        {
            TenantId = TenantA,
            ClaimId = claimId,
            SupersedesVersionId = Guid.NewGuid(),
            AudienceProfileIds = { Aud1 },
            ReviewRounds = { new ClaimReviewRound { WorkflowInstanceId = Guid.NewGuid() } }
        };
        var doc = version.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["ClaimId"].BsonType);
        Assert.Equal(claimId.ToString(), doc["ClaimId"].AsString);
        Assert.Equal(BsonType.String, doc["SupersedesVersionId"].BsonType);
        Assert.Equal(BsonType.String, doc["AudienceProfileIds"].AsBsonArray[0].BsonType);
        Assert.Equal(BsonType.String, doc["ReviewRounds"].AsBsonArray[0]["WorkflowInstanceId"].BsonType);
        Assert.Equal(BsonType.String, doc["TenantId"].BsonType);
        Assert.Equal(claimId, BsonSerializer.Deserialize<ClaimCountryVersion>(doc).ClaimId);

        var claim = new Claim
        {
            TenantId = TenantA, ProductId = Product, AudienceProfileIds = { Aud2 },
            ResponsibleOrgUnitId = Guid.NewGuid(), SupersedesClaimId = Guid.NewGuid()
        };
        var claimDoc = claim.ToBsonDocument();
        Assert.Equal(BsonType.String, claimDoc["ProductId"].BsonType);
        Assert.Equal(BsonType.String, claimDoc["AudienceProfileIds"].AsBsonArray[0].BsonType);
        Assert.Equal(BsonType.String, claimDoc["ResponsibleOrgUnitId"].BsonType);
        Assert.Equal(BsonType.String, claimDoc["SupersedesClaimId"].BsonType);
    }

    [Fact]
    public void Pre_v2_claim_document_reads_without_the_new_fields()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var legacy = new Claim
        {
            TenantId = TenantA, ClaimCode = "CL-OLDDOC", ClaimName = "Old", ClaimText = "Old text",
            ClaimVersion = "1.0", Status = ClaimStatuses.Approved, EffectiveFrom = Jan1
        }.ToBsonDocument();
        foreach (var v2 in new[]
                 {
                     "Kind", "LocalCountryCode", "ProductId", "ProductDisplay", "AudienceProfileIds",
                     "ResponsibleOrgUnitId", "TextLanguageCode", "SupersedesClaimId", "CountryClosures"
                 })
        {
            legacy.Remove(v2);
        }

        var read = BsonSerializer.Deserialize<Claim>(legacy);
        Assert.Equal("CL-OLDDOC", read.ClaimCode);
        Assert.Equal(ClaimKinds.Core, read.Kind);
        Assert.Null(read.ProductId);
        Assert.Empty(read.AudienceProfileIds);
        Assert.Empty(read.CountryClosures);
        Assert.Equal(ClaimKinds.Core, ClaimMapper.ToDto(read).Kind);
    }

    [Theory]
    [InlineData(nameof(ClaimsController.Coverage), "GET", "claims/coverage", ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.NewVersion), "POST", "claims/{claimId:guid}/new-version", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.CloseCountry), "POST", "claims/{claimId:guid}/country-closures", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.ReopenCountry), "POST", "claims/{claimId:guid}/country-closures/{countryCode}/reopen", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.ListCountryVersions), "GET", "claims/{claimId:guid}/country-versions", ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.CreateCountryVersion), "POST", "claims/{claimId:guid}/country-versions", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.GetCountryVersion), "GET", "claims/country-versions/{countryVersionId:guid}", ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.UpdateCountryVersion), "PUT", "claims/country-versions/{countryVersionId:guid}", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.NewCountryVersion), "POST", "claims/country-versions/{countryVersionId:guid}/new-version", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.ApproveCountryVersion), "POST", "claims/country-versions/{countryVersionId:guid}/approve", ClaimPermissions.Approve)]
    [InlineData(nameof(ClaimsController.ArchiveCountryVersion), "POST", "claims/country-versions/{countryVersionId:guid}/archive", ClaimPermissions.Manage)]
    public void V2_endpoints_use_the_claims_prefix_and_existing_permission_keys(
        string action, string verb, string route, string permission)
    {
        var method = typeof(ClaimsController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;
        var attribute = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Contains(verb, attribute.HttpMethods);
        Assert.Equal("api/crm/content-composition/" + route, attribute.Template);
        Assert.Contains(method.GetCustomAttribute<HasPermissionAttribute>()!.Permission, ClaimPermissions.All);
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
    }

    // ============================================================ fakes

    private sealed class FakeClaimRepo : IClaimRepository
    {
        public List<Claim> Items { get; } = new();

        public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.ClaimCode == code).ToList());

        public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.ClaimCode == code && !x.IsArchived()));

        public Task InsertAsync(Claim e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Claim e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeCountryVersionRepo : IClaimCountryVersionRepository
    {
        public List<ClaimCountryVersion> Items { get; } = new();

        private IEnumerable<ClaimCountryVersion> Of(Guid t) => Items.Where(x => x.TenantId == t && !x.IsDeleted);

        public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Of(t).FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Of(t).Where(x => x.ClaimCode == code).ToList());

        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Of(t).Where(x => x.ClaimId == claimId).ToList());

        public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Of(t).ToList());

        public Task InsertAsync(ClaimCountryVersion e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ClaimCountryVersion e, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>MOD-0048 published sets as the consumer seam sees them (test data only).</summary>
    private sealed class FakeReferences : IReferenceDataValidator, IReferenceMetadataReader, IReferenceDataCatalogReader
    {
        private static readonly Dictionary<string, string> Languages = new()
        {
            ["TR"] = "tr", ["BY"] = "ru,be", ["UZ"] = "uz,ru", ["TM"] = "tk", ["GE"] = "ka", ["AZ"] = "az"
        };

        public Dictionary<string, List<(string Code, Dictionary<string, string>? Attributes)>> Sets { get; } = new();

        public static IReadOnlyList<string> LanguagesOf(string country) => Languages[country.ToUpperInvariant()].Split(',');

        public static FakeReferences Default()
        {
            var refs = new FakeReferences();
            refs.Sets[ClaimReferenceSets.CountryCodes] = BrdCountries.Select(c => (c, (Dictionary<string, string>?)null)).ToList();
            refs.Sets[ClaimReferenceSets.CountryContentLanguages] = BrdCountries
                .Select(c => (c, (Dictionary<string, string>?)new Dictionary<string, string> { ["Languages"] = Languages[c] }))
                .ToList();
            refs.Sets[ClaimReferenceSets.ClosureReason] = new[] { "no-license", "regulation-disallows", "business-decision" }
                .Select(c => (c, (Dictionary<string, string>?)null)).ToList();
            refs.Sets[ClaimReferenceSets.AdaptationType] = new[] { "verbatim", "narrowed", "softened" }
                .Select(c => (c, (Dictionary<string, string>?)null)).ToList();
            return refs;
        }

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
        {
            if (!Sets.TryGetValue(setCode, out var values))
            {
                return Task.FromResult(new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value));
            }

            var ok = values.Any(v => string.Equals(v.Code, value, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(new ReferenceValidationResult(
                ok ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue, setCode, value));
        }

        public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(
            string setCode, string value, CancellationToken ct)
        {
            if (!Sets.TryGetValue(setCode, out var values))
            {
                return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
            }

            var hit = values.FirstOrDefault(v => string.Equals(v.Code, value, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(hit.Code is null
                ? null
                : new Dictionary<string, string>(hit.Attributes ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase));
        }

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken ct)
            => Task.FromResult(Sets.TryGetValue(setCode, out var values)
                ? new ReferenceSetSnapshot(setCode, true,
                    values.Select(v => new ReferenceValueSnapshot(v.Code, v.Code + " name", null, true, false,
                        v.Attributes)).ToList())
                : ReferenceSetSnapshot.NotPublished(setCode));
    }

    private sealed class FakeProducts : IStrategyTemplateProductReferenceValidator
    {
        public bool Unavailable { get; set; }

        public Task<IStrategyTemplateProductReferenceValidator.Outcome> ValidateAsync(
            string referenceKind, Guid referenceId, CancellationToken cancellationToken)
            => Task.FromResult(Unavailable
                ? IStrategyTemplateProductReferenceValidator.Outcome.Unavailable
                : referenceKind == IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct
                  && referenceId == Product
                    ? IStrategyTemplateProductReferenceValidator.Outcome.Valid
                    : IStrategyTemplateProductReferenceValidator.Outcome.NotFound);
    }

    private sealed class FakeAudiences : IAudienceProfileRepository
    {
        private readonly List<AudienceProfile> _items;

        public FakeAudiences(Guid tenant, params Guid[] ids)
            => _items = ids.Select(id => new AudienceProfile { Id = id, TenantId = tenant, ProfileCode = id.ToString() })
                .ToList();

        public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == t && x.Id == id));

        public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AudienceProfile>)_items.Where(x => x.TenantId == t).ToList());

        public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == t && x.ProfileCode == code));

        public Task InsertAsync(AudienceProfile profile, CancellationToken ct) { _items.Add(profile); return Task.CompletedTask; }
        public Task UpdateAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSettings : IClaimCoverageSettings
    {
        public int Days { get; set; } = ClaimCoverageDefaults.ExpiringWindowDays;
        public int ExpiringWindowDays => Days;
    }

    private sealed class CapturingAudit : IContentCompositionAuditPublisher
    {
        public List<(string Event, string EntityType, Guid EntityId, int Version, string? Detail)> Events { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version,
            string? detail, CancellationToken cancellationToken)
        {
            Events.Add((eventName, entityType, entityId, version, detail));
            return Task.CompletedTask;
        }
    }
}
