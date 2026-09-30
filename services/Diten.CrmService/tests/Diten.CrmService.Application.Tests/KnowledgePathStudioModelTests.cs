using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Path.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-KP-1 — the knowledge path studio model: chain + country + language identity, the derived context, step arrangement
/// on the chain's slots with the branch-first StepOrder, claims on the path and their read, chain conformance, the
/// legacy-path mark and the class map. Legacy (chain-less) paths keep the FU04 behaviour (KnowledgePathRuntimeTests).
/// </summary>
public sealed class KnowledgePathStudioModelTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProductX = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ================================================================ create + identity

    [Fact]
    public async Task Chained_create_takes_the_chain_subject_derives_the_audience_and_issues_a_KP_code()
    {
        var fx = new Fixture();
        var r = await fx.CreatePath().Handle(fx.CreateChained(pathCode: "", subjectId: Guid.NewGuid()), default);
        Assert.Equal(201, r.StatusCode);

        var path = fx.Paths.Items.Single();
        Assert.Equal(fx.Subject.Id, path.SubjectId);
        Assert.Equal("TR", path.CountryCode);
        Assert.Equal("tr", path.LanguageCode);
        Assert.Equal(fx.Template.Id, path.ChainTemplate!.ConceptChainTemplateId);
        Assert.Equal("1.0", path.ChainTemplate.ChainVersion);
        Assert.Equal(fx.Profile.Id, path.AudienceProfileId);            // single ForWhom → AudienceProfileId
        Assert.StartsWith("KP-", path.PathCode);                         // never an ISO-like prefix (BY-)
        Assert.False(path.IsLegacyUnapproved());
    }

    [Theory]
    [InlineData("XX", "tr", "country_invalid", 400)]
    [InlineData("TR", "en", "language_not_in_country", 400)]
    [InlineData("TR", null, "language_not_in_country", 400)]
    public async Task Chained_create_validates_country_and_language(string country, string? language, string code, int status)
    {
        var fx = new Fixture();
        var r = await fx.CreatePath().Handle(fx.CreateChained(country: country, language: language), default);
        Assert.Equal(status, r.StatusCode);
        Assert.Equal(code, r.Errors![0]);
        Assert.Empty(fx.Paths.Items);
    }

    [Fact]
    public async Task Chained_create_is_503_when_the_reference_sets_cannot_be_read()
    {
        var fx = new Fixture();
        fx.Catalog.Published = false;
        var r = await fx.CreatePath().Handle(fx.CreateChained(), default);
        Assert.Equal(503, r.StatusCode);
        Assert.Equal(ChainContextErrors.ReferenceSetUnavailable, r.Errors![0]);
    }

    [Fact]
    public async Task Chained_create_rejects_a_draft_a_flat_or_a_missing_chain()
    {
        var fx = new Fixture();
        foreach (var chainId in new Guid?[] { fx.DraftTemplate.Id, fx.FlatTemplate.Id, Guid.NewGuid() })
        {
            var r = await fx.CreatePath().Handle(fx.CreateChained(chainId: chainId), default);
            Assert.Equal(400, r.StatusCode);
            Assert.Equal(KnowledgePathStudioErrors.ChainTemplateInvalid, r.Errors![0]);
        }

        // a country without a chain is not a legacy create either
        var noChain = await fx.CreatePath().Handle(new CreateKnowledgePathCommand(
            "KP-X", "Path", fx.Subject.Id, "Obj", "1.0", Jan1, LanguageCode: "tr", CountryCode: "TR"), default);
        Assert.Equal(KnowledgePathStudioErrors.ChainTemplateInvalid, noChain.Errors![0]);
        Assert.Empty(fx.Paths.Items);
    }

    [Fact]
    public async Task Identity_is_locked_on_a_chained_path_country_language_and_subject()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();

        var language = await fx.UpdatePath().Handle(fx.Update(id, language: "en"), default);
        Assert.Equal(409, language.StatusCode);
        Assert.Equal(KnowledgePathStudioErrors.PathIdentityLocked, language.Errors![0]);

        var country = await fx.UpdatePath().Handle(fx.Update(id, country: "UZ"), default);
        Assert.Equal(409, country.StatusCode);
        Assert.Equal(KnowledgePathStudioErrors.PathIdentityLocked, country.Errors![0]);

        var subject = await fx.UpdatePath().Handle(fx.Update(id, subjectId: Guid.NewGuid()), default);
        Assert.Equal(KnowledgePathStudioErrors.ChainSubjectMismatch, subject.Errors![0]);

        // an omitted country / language keeps the identity; the same values pass
        Assert.True((await fx.UpdatePath().Handle(fx.Update(id, name: "Renamed"), default)).IsSuccessful);
        Assert.True((await fx.UpdatePath().Handle(fx.Update(id, name: "Renamed", language: "tr", country: "TR"), default)).IsSuccessful);
        var path = fx.Paths.Items.Single();
        Assert.Equal(("TR", "tr", "Renamed"), (path.CountryCode, path.LanguageCode, path.PathName));
    }

    [Fact]
    public async Task A_legacy_path_update_keeps_the_old_language_behaviour_and_takes_no_country()
    {
        var fx = new Fixture();
        var id = await fx.LegacyPathAsync("tr");
        Assert.True((await fx.UpdatePath().Handle(fx.Update(id, language: "en"), default)).IsSuccessful);
        Assert.Equal("en", fx.Paths.Items.Single().LanguageCode);

        var country = await fx.UpdatePath().Handle(fx.Update(id, country: "TR"), default);
        Assert.Equal(KnowledgePathStudioErrors.ChainTemplateRequired, country.Errors![0]);
    }

    // ================================================================ bind-chain

    [Fact]
    public async Task Bind_chain_binds_a_draft_legacy_path_once()
    {
        var fx = new Fixture();
        var id = await fx.LegacyPathAsync(null);

        var invalid = await fx.Bind().Handle(new BindKnowledgePathChainCommand(id, fx.Template.Id, "XX", "tr"), default);
        Assert.Equal((400, ChainContextErrors.CountryInvalid), (invalid.StatusCode, invalid.Errors![0]));
        Assert.True(fx.Paths.Items.Single().IsLegacyUnapproved());

        var ok = await fx.Bind().Handle(new BindKnowledgePathChainCommand(id, fx.Template.Id, "tr", "TR"), default);
        Assert.True(ok.IsSuccessful, string.Join("; ", ok.Errors ?? new List<string>()));

        var path = fx.Paths.Items.Single();
        Assert.Equal((fx.Template.Id, "TR", "tr"), (path.ChainTemplate!.ConceptChainTemplateId, path.CountryCode, path.LanguageCode));
        Assert.Equal(fx.Profile.Id, path.AudienceProfileId);

        var twice = await fx.Bind().Handle(new BindKnowledgePathChainCommand(id, fx.Template.Id, "TR", "tr"), default);
        Assert.Equal(409, twice.StatusCode);
        Assert.Equal(KnowledgePathStudioErrors.PathIdentityLocked, twice.Errors![0]);
    }

    [Fact]
    public async Task Bind_chain_refuses_another_subject_a_non_draft_path_and_foreign_language_steps()
    {
        var fx = new Fixture();

        var otherSubject = await fx.LegacyPathAsync("tr", subjectId: fx.OtherSubject.Id);
        var mismatch = await fx.Bind().Handle(new BindKnowledgePathChainCommand(otherSubject, fx.Template.Id, "TR", "tr"), default);
        Assert.Equal(409, mismatch.StatusCode);
        Assert.Equal(KnowledgePathStudioErrors.ChainSubjectMismatch, mismatch.Errors![0]);

        var published = await fx.LegacyPathAsync("tr", code: "KP-PUB");
        await fx.AddLegacyStep(published, 10, fx.ContentTr.Id);
        Assert.True((await fx.Publish().Handle(new PublishKnowledgePathCommand(published), default)).IsSuccessful);
        Assert.Equal(409, (await fx.Bind().Handle(new BindKnowledgePathChainCommand(published, fx.Template.Id, "TR", "tr"), default)).StatusCode);

        var english = await fx.LegacyPathAsync(null, code: "KP-EN");
        await fx.AddLegacyStep(english, 10, fx.ContentEn.Id);
        var language = await fx.Bind().Handle(new BindKnowledgePathChainCommand(english, fx.Template.Id, "TR", "tr"), default);
        Assert.Equal(409, language.StatusCode);
        Assert.Equal(ChainContextErrors.ComponentLanguageMismatch, language.Errors![0]);

        var draftChain = await fx.Bind().Handle(new BindKnowledgePathChainCommand(english, fx.DraftTemplate.Id, "TR", "tr"), default);
        Assert.Equal(KnowledgePathStudioErrors.ChainTemplateInvalid, draftChain.Errors![0]);
    }

    // ================================================================ new version

    [Fact]
    public async Task New_version_copies_the_identity_the_chain_the_arrangement_and_the_claims()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        Assert.Equal(201, (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1)).StatusCode);
        var claim = fx.SeedClaim("CLM-1");
        Assert.True((await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, claim.Id, Slot("BR1", fx.T2)), default)).IsSuccessful);
        Assert.True((await fx.Publish().Handle(new PublishKnowledgePathCommand(id), default)).IsSuccessful);

        var r = await fx.NewVersion().Handle(new CreateKnowledgePathVersionCommand(id), default);
        Assert.Equal(201, r.StatusCode);
        var clone = fx.Paths.Items.Single(p => p.Id == r.Data);
        Assert.Equal((fx.Template.Id, "1.0"), (clone.ChainTemplate!.ConceptChainTemplateId, clone.ChainTemplate.ChainVersion));
        Assert.Equal(("TR", "tr"), (clone.CountryCode, clone.LanguageCode));
        Assert.Equal(fx.T1, clone.Steps.Single().Arrangement!.ChainStepId);
        Assert.Equal("CLM-1", clone.Claims.Single().ClaimCode);
        Assert.NotSame(fx.Paths.Items.Single(p => p.Id == id).ChainTemplate, clone.ChainTemplate);
    }

    // ================================================================ arrangement

    [Fact]
    public async Task A_step_off_the_chain_is_chain_slot_invalid()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();

        foreach (var arrangement in new[]
                 {
                     null, Slot("BR9", fx.T1), Slot("BR1", fx.T3) /* T3 lives in BR2 */, Slot("BR1", fx.T1, -1)
                 })
        {
            var r = await fx.AddStep(id, fx.ContentTr.Id, arrangement);
            Assert.Equal(400, r.StatusCode);
            Assert.Equal(KnowledgePathStudioErrors.ChainSlotInvalid, r.Errors![0]);
        }

        Assert.Empty(fx.Paths.Items.Single().Steps);
    }

    [Fact]
    public async Task A_full_slot_is_chain_slot_full()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        Assert.Equal(201, (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1)).StatusCode); // T1 max 1

        var second = await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1, code: "S2");
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(KnowledgePathStudioErrors.ChainSlotFull, second.Errors![0]);
    }

    [Fact]
    public async Task A_placed_step_cannot_move_to_another_slot_only_its_position_changes()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        var stepId = (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T2)).Data;

        var moved = await fx.UpdateStep(id, stepId, Slot("BR2", fx.T3));
        Assert.Equal(409, moved.StatusCode);
        Assert.Equal(KnowledgePathStudioErrors.ChainSlotMoveForbidden, moved.Errors![0]);

        var repositioned = await fx.UpdateStep(id, stepId, Slot("br1", fx.T2, 5));
        Assert.True(repositioned.IsSuccessful);
        Assert.Equal(5, fx.Paths.Items.Single().Steps.Single().Arrangement!.Position);
    }

    [Fact]
    public async Task A_legacy_path_takes_no_arrangement()
    {
        var fx = new Fixture();
        var id = await fx.LegacyPathAsync("tr");
        var r = await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1);
        Assert.Equal(409, r.StatusCode);
        Assert.Equal(KnowledgePathStudioErrors.ChainTemplateRequired, r.Errors![0]);
    }

    [Fact]
    public async Task A_concept_node_of_another_type_is_chain_slot_invalid()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        var other = fx.SeedNode(fx.T2);
        var same = fx.SeedNode(fx.T1);

        var bad = await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1, node: other.Id);
        Assert.Equal(KnowledgePathStudioErrors.ChainSlotInvalid, bad.Errors![0]);
        Assert.Equal(201, (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1, node: same.Id)).StatusCode);
    }

    [Fact]
    public async Task Chained_path_content_must_be_in_the_path_language_legacy_path_keeps_cross_language()
    {
        var fx = new Fixture();
        var chained = await fx.ChainedPathAsync();
        var mismatch = await fx.AddStep(chained, fx.ContentEn.Id, "BR1", fx.T1);
        Assert.Equal(409, mismatch.StatusCode);
        Assert.Equal(ChainContextErrors.ComponentLanguageMismatch, mismatch.Errors![0]);

        // update: switching a placed step to foreign-language content is refused too
        var stepId = (await fx.AddStep(chained, fx.ContentTr.Id, "BR1", fx.T1)).Data;
        var switched = await fx.UpdateStep(chained, stepId, Slot("BR1", fx.T1), contentId: fx.ContentEn.Id);
        Assert.Equal(ChainContextErrors.ComponentLanguageMismatch, switched.Errors![0]);

        var legacy = await fx.LegacyPathAsync("tr", code: "KP-OLD");
        Assert.Equal(201, (await fx.AddLegacyStep(legacy, 10, fx.ContentEn.Id)).StatusCode);
        var dto = (await fx.GetPath().Handle(new GetKnowledgePathQuery(legacy), default)).Data!;
        Assert.True(dto.Steps.Single().IsCrossLanguageStep);
    }

    [Fact]
    public async Task StepOrder_is_computed_branch_first_and_the_client_value_is_ignored()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        var t1 = (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1, order: 999, code: "A")).Data;
        var t2b = (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T2, order: 1, code: "B", position: 1)).Data;
        var t2a = (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T2, order: 1, code: "C", position: 0)).Data;
        var t3 = (await fx.AddStep(id, fx.ContentTr.Id, "BR2", fx.T3, order: 5, code: "D")).Data;

        // BR2 (SortOrder 0) first, then BR1 (SortOrder 1): T1, then T2 by Position.
        var order = fx.Paths.Items.Single().OrderedActiveSteps().Select(s => s.StepId).ToList();
        Assert.Equal(new[] { t3, t1, t2a, t2b }, order);
        Assert.Equal(new[] { 10, 20, 30, 40 }, fx.Paths.Items.Single().OrderedActiveSteps().Select(s => s.StepOrder));

        // a position change re-orders within the slot
        Assert.True((await fx.UpdateStep(id, t2b, Slot("BR1", fx.T2, 0), code: "B")).IsSuccessful);
        Assert.True((await fx.UpdateStep(id, t2a, Slot("BR1", fx.T2, 3), code: "C")).IsSuccessful);
        Assert.Equal(new[] { t3, t1, t2b, t2a }, fx.Paths.Items.Single().OrderedActiveSteps().Select(s => s.StepId));
    }

    [Fact]
    public async Task A_reposition_may_not_put_a_step_before_its_prerequisite()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        var first = (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T2, code: "A", position: 0)).Data;
        var second = (await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T2, code: "B", position: 1, prereq: first)).Data;

        var r = await fx.UpdateStep(id, first, Slot("BR1", fx.T2, 9), code: "A");
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(first, fx.Paths.Items.Single().OrderedActiveSteps()[0].StepId);
        Assert.NotEqual(Guid.Empty, second);
    }

    // ================================================================ claims

    [Fact]
    public async Task Claims_are_added_arranged_and_removed_on_a_draft_chained_path()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        var a = fx.SeedClaim("CLM-A");
        var b = fx.SeedClaim("CLM-B");

        Assert.True((await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, a.Id, Slot("BR1", fx.T1)), default)).IsSuccessful);
        Assert.True((await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, b.Id, Slot("BR1", fx.T1, 1)), default)).IsSuccessful);
        Assert.True((await fx.ArrangeClaim().Handle(new ArrangeKnowledgePathClaimCommand(id, a.Id, 2), default)).IsSuccessful);

        var dto = (await fx.GetPath().Handle(new GetKnowledgePathQuery(id), default)).Data!;
        Assert.Equal(new[] { "CLM-B", "CLM-A" }, dto.Claims!.Select(c => c.ClaimCode));

        Assert.True((await fx.RemoveClaim().Handle(new RemoveKnowledgePathClaimCommand(id, b.Id), default)).IsSuccessful);
        Assert.Equal("CLM-A", fx.Paths.Items.Single().Claims.Single().ClaimCode);
        Assert.Equal(404, (await fx.RemoveClaim().Handle(new RemoveKnowledgePathClaimCommand(id, b.Id), default)).StatusCode);
    }

    [Fact]
    public async Task Claim_rules_product_duplicate_tenant_slot_legacy_and_frozen()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        var mine = fx.SeedClaim("CLM-1");
        var foreign = fx.SeedClaim("CLM-2", product: Guid.NewGuid());

        var product = await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, foreign.Id, Slot("BR1", fx.T1)), default);
        Assert.Equal((409, KnowledgeContentClaimErrors.ClaimProductMismatch), (product.StatusCode, product.Errors![0]));

        Assert.True((await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, mine.Id, Slot("BR1", fx.T1)), default)).IsSuccessful);
        var duplicate = await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, mine.Id, Slot("BR2", fx.T3)), default);
        Assert.Equal((409, KnowledgeContentClaimErrors.ClaimRefDuplicate), (duplicate.StatusCode, duplicate.Errors![0]));

        var missing = await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, Guid.NewGuid(), Slot("BR1", fx.T1)), default);
        Assert.Equal((404, KnowledgeContentClaimErrors.ClaimNotFound), (missing.StatusCode, missing.Errors![0]));

        var slot = await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(id, fx.SeedClaim("CLM-3").Id, Slot("BR1", fx.T3)), default);
        Assert.Equal((400, KnowledgePathStudioErrors.ChainSlotInvalid), (slot.StatusCode, slot.Errors![0]));

        var legacy = await fx.LegacyPathAsync("tr", code: "KP-OLD");
        var noChain = await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(legacy, mine.Id, Slot("BR1", fx.T1)), default);
        Assert.Equal((409, KnowledgePathStudioErrors.ChainTemplateRequired), (noChain.StatusCode, noChain.Errors![0]));

        await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T1);
        Assert.True((await fx.Publish().Handle(new PublishKnowledgePathCommand(id), default)).IsSuccessful);
        Assert.Equal(409, (await fx.RemoveClaim().Handle(new RemoveKnowledgePathClaimCommand(id, mine.Id), default)).StatusCode);
    }

    [Fact]
    public async Task Claim_read_resolves_the_country_version_by_coverage_priority_and_the_path_language_text()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();

        var usable = fx.SeedClaim("CLM-OK", name: "Etkinlik");
        fx.SeedVersion(usable, "TR", ClaimStatuses.Approved, "1.0", ("tr", "Türkçe metin"), ("en", "English text"),
            qualifier: ("tr", "Türkçe dipnot"));
        fx.SeedVersion(usable, "TR", ClaimStatuses.Draft, "2.0", ("tr", "Taslak metin"));   // newer, but a draft
        fx.SeedVersion(usable, "UZ", ClaimStatuses.Approved, "1.0", ("uz", "Uzbek"));        // another country
        var draftOnly = fx.SeedClaim("CLM-DRAFT");
        fx.SeedVersion(draftOnly, "TR", ClaimStatuses.Draft, "1.0", ("tr", "Taslak"));
        var noCountry = fx.SeedClaim("CLM-NONE");
        fx.SeedVersion(noCountry, "UZ", ClaimStatuses.Approved, "1.0", ("uz", "Uzbek"));
        var noLanguage = fx.SeedClaim("CLM-LANG");
        fx.SeedVersion(noLanguage, "TR", ClaimStatuses.Approved, "1.0", ("en", "English only"));

        var position = 0;
        foreach (var claim in new[] { usable, draftOnly, noCountry, noLanguage })
        {
            Assert.True((await fx.AddClaim().Handle(new AddKnowledgePathClaimCommand(
                id, claim.Id, Slot("BR1", fx.T1, position++)), default)).IsSuccessful);
        }

        var claims = (await fx.GetPath().Handle(new GetKnowledgePathQuery(id), default)).Data!.Claims!
            .ToDictionary(c => c.ClaimCode);

        var ok = claims["CLM-OK"];
        Assert.Equal(("Etkinlik", "Türkçe metin", "Türkçe dipnot", "1.0", ClaimStatuses.Approved),
            (ok.Name, ok.Text, ok.Qualifier, ok.CountryVersion, ok.Status));
        Assert.True(ok.Usable);
        Assert.Null(ok.Reason);
        Assert.NotNull(ok.CountryVersionId);

        Assert.Equal((false, KnowledgePathClaimReasons.NotApproved), (claims["CLM-DRAFT"].Usable, claims["CLM-DRAFT"].Reason));
        Assert.Equal((false, KnowledgePathClaimReasons.NoCountryVersion), (claims["CLM-NONE"].Usable, claims["CLM-NONE"].Reason));
        Assert.Null(claims["CLM-NONE"].CountryVersionId);
        Assert.Equal((false, KnowledgePathClaimReasons.LanguageMismatch), (claims["CLM-LANG"].Usable, claims["CLM-LANG"].Reason));
        Assert.Null(claims["CLM-LANG"].Text);
    }

    // ================================================================ reads

    [Fact]
    public async Task Detail_carries_the_chain_the_derived_context_and_the_chain_conformance()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();
        await fx.AddStep(id, fx.ContentTr.Id, "BR1", fx.T2, code: "A");

        var dto = (await fx.GetPath().Handle(new GetKnowledgePathQuery(id), default)).Data!;
        Assert.Equal((fx.Template.Id, "TPL-ALM", "Almiba", "1.0"),
            (dto.ChainTemplate!.Id, dto.ChainTemplate.Code, dto.ChainTemplate.Name, dto.ChainTemplate.Version));
        Assert.Equal(("TR", "tr"), (dto.CountryCode, dto.LanguageCode));
        Assert.Equal((ProductX, "ALMIBA"), (dto.DerivedContext!.ProductId, dto.DerivedContext.ProductCode));
        Assert.Equal(fx.Profile.Id, Assert.Single(dto.DerivedContext.AudienceProfileIds));
        Assert.False(dto.IsLegacyUnapproved);
        Assert.True(dto.IdentityLocked);
        Assert.Equal("BR1", dto.Steps.Single().Arrangement!.BranchCode);

        var conformance = dto.ChainConformance!;
        Assert.Equal(new[] { ("BR2", fx.T3), ("BR1", fx.T1), ("BR1", fx.T2) },
            conformance.Select(c => (c.BranchCode, c.ChainStepId)));
        Assert.Equal(KnowledgePathConformanceStatuses.Ok, conformance[0].Status);    // T3 min 0
        Assert.Equal(KnowledgePathConformanceStatuses.Under, conformance[1].Status); // T1 min 1, none
        Assert.Equal((1, 1, 2, KnowledgePathConformanceStatuses.Ok),
            (conformance[2].Count, conformance[2].Min, conformance[2].Max, conformance[2].Status));
        Assert.Equal("Type T1", conformance[1].Name);

        // over: a stored document beyond the max (e.g. a pre-existing one) is reported, never hidden
        var path = fx.Paths.Items.Single();
        foreach (var n in new[] { 1, 2 })
        {
            path.Steps.Add(new KnowledgePathStep
            {
                StepCode = "X" + n, StepOrder = 100 + n, ContentId = fx.ContentTr.Id,
                Arrangement = new KnowledgePathArrangement { BranchCode = "BR1", ChainStepId = fx.T2, Position = n }
            });
        }

        var over = (await fx.GetPath().Handle(new GetKnowledgePathQuery(id), default)).Data!.ChainConformance![2];
        Assert.Equal((3, KnowledgePathConformanceStatuses.Over), (over.Count, over.Status));
    }

    [Fact]
    public async Task A_legacy_path_reads_as_unapproved_legacy_in_detail_and_list()
    {
        var fx = new Fixture();
        var legacy = await fx.LegacyPathAsync("tr");
        var chained = await fx.ChainedPathAsync();

        var dto = (await fx.GetPath().Handle(new GetKnowledgePathQuery(legacy), default)).Data!;
        Assert.True(dto.IsLegacyUnapproved);
        Assert.False(dto.IdentityLocked);
        Assert.Null(dto.ChainTemplate);
        Assert.Null(dto.DerivedContext);
        Assert.Empty(dto.Claims!);
        Assert.Empty(dto.ChainConformance!);

        var list = (await fx.ListPaths().Handle(new ListKnowledgePathsQuery(), default)).Data!.Items.ToDictionary(i => i.PathId);
        Assert.True(list[legacy].IsLegacyUnapproved);
        Assert.Null(list[legacy].ChainTemplateCode);
        Assert.Equal((false, "TPL-ALM", "TR", "tr"),
            (list[chained].IsLegacyUnapproved, list[chained].ChainTemplateCode, list[chained].CountryCode, list[chained].LanguageCode));
    }

    // ================================================================ class map

    [Fact]
    public void The_studio_fields_round_trip_as_string_guids_and_a_pre_kp1_document_reads_as_legacy()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var chainId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var path = new KnowledgePath
        {
            TenantId = TenantA, PathCode = "KP-2026-ABCDEF", PathName = "P", CountryCode = "TR", LanguageCode = "tr",
            ChainTemplate = new KnowledgePathChainRef { ConceptChainTemplateId = chainId, ChainVersion = "1.0" },
            Claims = { new KnowledgePathClaim { ClaimId = claimId, ClaimCode = "CLM-1",
                Arrangement = new KnowledgePathArrangement { BranchCode = "BR1", ChainStepId = typeId, Position = 2 } } },
            Steps = { new KnowledgePathStep { StepCode = "S1",
                Arrangement = new KnowledgePathArrangement { BranchCode = "BR1", ChainStepId = typeId, Position = 1 } } }
        };

        var doc = path.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["ChainTemplate"]["ConceptChainTemplateId"].BsonType);
        Assert.Equal(BsonType.String, doc["Claims"][0]["ClaimId"].BsonType);
        Assert.Equal(BsonType.String, doc["Claims"][0]["Arrangement"]["ChainStepId"].BsonType);
        Assert.Equal(BsonType.String, doc["Steps"][0]["Arrangement"]["ChainStepId"].BsonType);

        var back = BsonSerializer.Deserialize<KnowledgePath>(doc);
        Assert.Equal((chainId, "TR", claimId, typeId, 1),
            (back.ChainTemplate!.ConceptChainTemplateId, back.CountryCode, back.Claims.Single().ClaimId,
                back.Steps.Single().Arrangement!.ChainStepId, back.Steps.Single().Arrangement!.Position));

        // a pre-KP-1 document has none of the new elements
        var old = path.ToBsonDocument();
        old.Remove("ChainTemplate");
        old.Remove("CountryCode");
        old.Remove("Claims");
        old["Steps"][0].AsBsonDocument.Remove("Arrangement");
        var legacy = BsonSerializer.Deserialize<KnowledgePath>(old);
        Assert.True(legacy.IsLegacyUnapproved());
        Assert.Empty(legacy.Claims);
        Assert.Null(legacy.Steps.Single().Arrangement);
    }

    // ================================================================ fixture

    private static KnowledgePathArrangementInput Slot(string branch, Guid step, int position = 0)
        => new(step, branch, position);

    private static TenantContext Tenant()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(TenantA);
        return ctx;
    }

    private sealed class Fixture
    {
        public PathRepo Paths { get; } = new();
        public ContentSetTestSubjects Subjects { get; } = new();
        public TopicRepo Topics { get; } = new();
        public ContentSetTestProfiles Profiles { get; } = new();
        public ContentSetTestTemplates Templates { get; } = new();
        public ContentSetTestContents Contents { get; } = new();
        public ContentSetTestClaims Claims { get; } = new();
        public VersionRepo Versions { get; } = new();
        public NodeRepo Nodes { get; } = new();
        public TypeRepo Types { get; } = new();
        public ContentSetTestCatalog Catalog { get; } = new();

        public Guid T1 { get; } = Guid.NewGuid();
        public Guid T2 { get; } = Guid.NewGuid();
        public Guid T3 { get; } = Guid.NewGuid();
        public Subject Subject { get; }
        public Subject OtherSubject { get; }
        public AudienceProfile Profile { get; }
        public ConceptChainTemplate Template { get; }
        public ConceptChainTemplate DraftTemplate { get; }
        public ConceptChainTemplate FlatTemplate { get; }
        public KnowledgeContent ContentTr { get; }
        public KnowledgeContent ContentEn { get; }

        public Fixture()
        {
            Subject = new Subject
            {
                TenantId = TenantA, SubjectCode = "ALM", SubjectName = "Almiba",
                ExternalReferences =
                {
                    new KnowledgeExternalReference
                    {
                        SourceSystem = "global-product", ExternalId = ProductX.ToString(), ExternalCode = "ALMIBA",
                        ExternalName = "Almiba 1 g", IsPrimary = true
                    }
                }
            };
            OtherSubject = new Subject { TenantId = TenantA, SubjectCode = "OTH", SubjectName = "Other" };
            Subjects.Items.AddRange(new[] { Subject, OtherSubject });
            Profile = new AudienceProfile { TenantId = TenantA, ProfileCode = "AUDP-NEF", ProfileName = "Nefroloji" };
            Profiles.Items.Add(Profile);
            foreach (var (id, name) in new[] { (T1, "Type T1"), (T2, "Type T2"), (T3, "Type T3") })
            {
                Types.Items.Add(new ConceptType { Id = id, TenantId = TenantA, ConceptTypeName = name, SubjectId = Subject.Id });
            }

            Template = new ConceptChainTemplate
            {
                TenantId = TenantA, ChainCode = "TPL-ALM", ChainName = "Almiba", SubjectId = Subject.Id, ChainVersion = "1.0",
                Status = ConceptChainStatuses.Published, EffectiveFrom = Jan1,
                OrderedConceptTypes = { T1, T2, T3 },
                ForWhomAudienceProfileIds = { Profile.Id },
                Branches =
                {
                    new ConceptChainBranch
                    {
                        BranchCode = "BR1", SortOrder = 1,
                        Steps =
                        {
                            new ConceptChainStep { ConceptTypeId = T1, MinSelection = 1, MaxSelection = 1 },
                            new ConceptChainStep { ConceptTypeId = T2, MinSelection = 1, MaxSelection = 2 }
                        }
                    },
                    new ConceptChainBranch
                    {
                        BranchCode = "BR2", SortOrder = 0,
                        Steps = { new ConceptChainStep { ConceptTypeId = T3, MinSelection = 0, MaxSelection = null } }
                    }
                }
            };
            DraftTemplate = new ConceptChainTemplate
            {
                TenantId = TenantA, ChainCode = "TPL-DRAFT", SubjectId = Subject.Id, ChainVersion = "1.0",
                Status = ConceptChainStatuses.Draft, OrderedConceptTypes = { T1, T2 }, Branches = Template.Branches
            };
            FlatTemplate = new ConceptChainTemplate
            {
                TenantId = TenantA, ChainCode = "TPL-FLAT", SubjectId = Subject.Id, ChainVersion = "1.0",
                Status = ConceptChainStatuses.Published, OrderedConceptTypes = { T1, T2 }
            };
            Templates.Items.AddRange(new[] { Template, DraftTemplate, FlatTemplate });
            ContentTr = SeedContent("KC-TR", "tr");
            ContentEn = SeedContent("KC-EN", "en");
        }

        private ChainContextResolver Resolver() => new(Templates, Subjects, Profiles);

        public CreateKnowledgePathHandler CreatePath()
            => new(Tenant(), new NullActorContext(), Paths, Subjects, Topics, Profiles, Templates, Catalog, Resolver());
        public UpdateKnowledgePathHandler UpdatePath()
            => new(Tenant(), new NullActorContext(), Paths, Subjects, Topics, Profiles);
        public PublishKnowledgePathHandler Publish() => new(Tenant(), new NullActorContext(), Paths);
        public CreateKnowledgePathVersionHandler NewVersion() => new(Tenant(), new NullActorContext(), Paths);
        public BindKnowledgePathChainHandler Bind()
            => new(Tenant(), new NullActorContext(), Paths, Templates, Contents, Resolver(), Catalog);
        public AddKnowledgePathStepHandler AddStepHandler()
            => new(Tenant(), new NullActorContext(), Paths, Contents, Nodes, Templates);
        public UpdateKnowledgePathStepHandler UpdateStepHandler()
            => new(Tenant(), new NullActorContext(), Paths, Contents, Nodes, Templates);
        public AddKnowledgePathClaimHandler AddClaim()
            => new(Tenant(), new NullActorContext(), Paths, Templates, Subjects, Claims);
        public ArrangeKnowledgePathClaimHandler ArrangeClaim() => new(Tenant(), new NullActorContext(), Paths);
        public RemoveKnowledgePathClaimHandler RemoveClaim() => new(Tenant(), new NullActorContext(), Paths);
        public GetKnowledgePathHandler GetPath()
            => new(Tenant(), Paths, Contents, Nodes, new KnowledgePathStudioReader(Templates, Resolver(), Claims, Versions, Types));
        public ListKnowledgePathsHandler ListPaths() => new(Tenant(), Paths, Contents, Nodes, Templates);

        public CreateKnowledgePathCommand CreateChained(
            string pathCode = "KP-1", Guid? subjectId = null, Guid? chainId = null, string country = "TR",
            string? language = "tr")
            => new(pathCode, "Path", subjectId ?? Guid.Empty, "Objective", "1.0", Jan1,
                LanguageCode: language, ChainTemplateId: chainId ?? Template.Id, CountryCode: country);

        public UpdateKnowledgePathCommand Update(
            Guid id, string name = "Path", Guid? subjectId = null, string? language = null, string? country = null)
        {
            var path = Paths.Items.Single(p => p.Id == id);
            return new UpdateKnowledgePathCommand(id, name, subjectId ?? path.SubjectId, "Objective", path.PathVersion,
                path.EffectiveFrom, LanguageCode: language, CountryCode: country);
        }

        public async Task<Guid> ChainedPathAsync()
        {
            var r = await CreatePath().Handle(CreateChained(pathCode: "KP-" + Guid.NewGuid().ToString("N")[..6]), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public async Task<Guid> LegacyPathAsync(string? language, Guid? subjectId = null, string code = "KP-LEG")
        {
            var r = await CreatePath().Handle(new CreateKnowledgePathCommand(
                code, "Legacy", subjectId ?? Subject.Id, "Objective", "1.0", Jan1, LanguageCode: language), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public Task<Response<Guid>> AddStep(
            Guid pathId, Guid contentId, string branch, Guid step, int order = 10, string? code = null, int position = 0,
            Guid? node = null, Guid? prereq = null)
            => AddStep(pathId, contentId, Slot(branch, step, position), order, code, node, prereq);

        public Task<Response<Guid>> AddStep(
            Guid pathId, Guid contentId, KnowledgePathArrangementInput? arrangement, int order = 10, string? code = null,
            Guid? node = null, Guid? prereq = null)
            => AddStepHandler().Handle(new AddKnowledgePathStepCommand(
                pathId, order, code ?? ("S" + Guid.NewGuid().ToString("N")[..4]), "Step", "core-message", contentId, true,
                PrerequisiteStepId: prereq, ConceptNodeId: node, Arrangement: arrangement), default);

        public Task<Response<Guid>> AddLegacyStep(Guid pathId, int order, Guid contentId)
            => AddStepHandler().Handle(new AddKnowledgePathStepCommand(
                pathId, order, "S" + order, "Step", "core-message", contentId, true), default);

        public Task<Response<bool>> UpdateStep(
            Guid pathId, Guid stepId, KnowledgePathArrangementInput arrangement, Guid? contentId = null, string? code = null)
        {
            var step = Paths.Items.Single(p => p.Id == pathId).Steps.Single(s => s.StepId == stepId);
            return UpdateStepHandler().Handle(new UpdateKnowledgePathStepCommand(
                pathId, stepId, 1, code ?? step.StepCode, "Step", "core-message", contentId ?? step.ContentId, true,
                PrerequisiteStepId: step.PrerequisiteStepId, Arrangement: arrangement), default);
        }

        public KnowledgeContent SeedContent(string code, string language)
        {
            var content = new KnowledgeContent
            {
                TenantId = TenantA, ContentCode = code, ContentTitle = code, ContentType = KnowledgeContentTypes.Presentation,
                ContentStatus = KnowledgeContentStatuses.Published, SubjectId = Subject.Id, LanguageCode = language,
                ContentVersion = "1.0", EffectiveFrom = Jan1, Url = "https://x"
            };
            Contents.Items.Add(content);
            return content;
        }

        public ConceptNode SeedNode(Guid type)
        {
            var node = new ConceptNode
            {
                TenantId = TenantA, SubjectId = Subject.Id, ConceptTypeId = type, ConceptNodeCode = "N-" + Guid.NewGuid().ToString("N")[..4],
                ConceptNodeName = "Node", Status = ConceptStatuses.Active, EffectiveFrom = Jan1
            };
            Nodes.Items.Add(node);
            return node;
        }

        public Claim SeedClaim(string code, Guid? product = null, string? name = null)
        {
            var claim = new Claim
            {
                TenantId = TenantA, ClaimCode = code, ClaimName = name ?? code, ClaimText = "core",
                ProductId = product ?? ProductX, Status = ClaimStatuses.Approved
            };
            Claims.Items.Add(claim);
            return claim;
        }

        public void SeedVersion(
            Claim claim, string country, string status, string version, (string Lang, string Text) text,
            (string Lang, string Text)? second = null, (string Lang, string Text)? qualifier = null)
        {
            var v = new ClaimCountryVersion
            {
                TenantId = TenantA, ClaimCode = claim.ClaimCode, ClaimId = claim.Id, CountryCode = country,
                CountryVersion = version, Status = status,
                Texts = { new ClaimLocalizedText { LanguageCode = text.Lang, Text = text.Text } }
            };
            if (second is { } s)
            {
                v.Texts.Add(new ClaimLocalizedText { LanguageCode = s.Lang, Text = s.Text });
            }

            if (qualifier is { } q)
            {
                v.Qualifiers.Add(new ClaimLocalizedText { LanguageCode = q.Lang, Text = q.Text });
            }

            Versions.Items.Add(v);
        }
    }

    private sealed class PathRepo : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();
        public Task<KnowledgePath?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(x => x.TenantId == t && x.PathCode == code).ToList());
        public Task InsertAsync(KnowledgePath e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath e, int expectedVersion, CancellationToken ct)
        {
            var stored = Items.FirstOrDefault(x => x.Id == e.Id && x.TenantId == e.TenantId);
            if (stored is null || stored.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            e.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class TopicRepo : ITopicRepository
    {
        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)Array.Empty<Topic>());
        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)Array.Empty<Topic>());
        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
            => Task.FromResult<Topic?>(null);
        public Task InsertAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NodeRepo : IConceptNodeRepository
    {
        public List<ConceptNode> Items { get; } = new();
        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items.Where(x => x.TenantId == t && x.SubjectId == s).ToList());
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid s, Guid ty, string code, CancellationToken ct)
            => Task.FromResult<ConceptNode?>(null);
        public Task InsertAsync(ConceptNode e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptNode e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TypeRepo : IConceptTypeRepository
    {
        public List<ConceptType> Items { get; } = new();
        public Task<ConceptType?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ConceptType>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptType>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<ConceptType>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptType>)Items.Where(x => x.TenantId == t && x.SubjectId == s).ToList());
        public Task<ConceptType?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
            => Task.FromResult<ConceptType?>(null);
        public Task InsertAsync(ConceptType e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptType e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class VersionRepo : IClaimCountryVersionRepository
    {
        public List<ClaimCountryVersion> Items { get; } = new();
        private IEnumerable<ClaimCountryVersion> Of(Guid t) => Items.Where(x => x.TenantId == t);
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
}
