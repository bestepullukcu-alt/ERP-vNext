using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// WP-SB-3a (DESIGN-SB-3 §3.1 / S3-2) — a product line says how it is told (promo / non-promo) and with which journey:
/// both required on a write; the journey must be published and tell the line's product (the subject's primary global
/// product, the KP-1 single definition); a journey in a language outside the template's country scope is a read-time
/// WARNING. A pre-SB-3a line still reads (promo, journey missing) and a pre-SB-3a play stays renameable; it is completed
/// through a new version, never in place. A NEW template-level knowledge-path / journey binding is refused, an existing
/// one stays readable (retired).
/// </summary>
public sealed class StrategyTemplateProductLineRoleJourneyTests
{
    private static readonly Guid TenantA = StrategyTemplateTestDoubles.TenantA;
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();

    private StrategyTemplateBindingValidator Bindings() => new(_segments, _policies, _paths, _journeys, _journeys.Subjects);

    private CreateStrategyTemplateHandler Create() => new(
        StrategyTemplateTestDoubles.Tenant(TenantA), new NullActorContext(), _templates, Bindings(), _references,
        StrategyTemplateTestDoubles.DefaultScope());

    private UpdateStrategyTemplateHandler Update() => new(
        StrategyTemplateTestDoubles.Tenant(TenantA), new NullActorContext(), _templates, Bindings(), _references,
        StrategyTemplateTestDoubles.DefaultScope());

    private GetStrategyTemplateByIdHandler Detail(IReferenceDataCatalogReader? catalog = null) => new(
        StrategyTemplateTestDoubles.Tenant(TenantA), _templates, new StrategyTemplateLineJourneyReader(_journeys, catalog));

    private async Task<(int Status, IReadOnlyList<string>? Errors, Guid Id)> CreateWith(
        params StrategyTemplateProductLineInput[] lines)
    {
        var segment = _segments.Add(TenantA);
        var response = await Create().Handle(StrategyTemplateTestBuilders.NewTemplate(segment.Id, productLines: lines), default);
        return (response.StatusCode, response.Errors, response.Data);
    }

    // ================================================================ write: role + journey required

    [Fact]
    public async Task A_written_line_needs_a_valid_role()
    {
        var missing = await CreateWith(StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), role: null));
        Assert.Equal(400, missing.Status);
        Assert.Contains(StrategyTemplateErrorCodes.ProductLineRoleRequired, missing.Errors!);

        var invalid = await CreateWith(StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), role: "hero"));
        Assert.Equal(400, invalid.Status);
        Assert.Contains(StrategyTemplateErrorCodes.ProductLineRoleInvalid, invalid.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task A_written_line_needs_a_journey()
    {
        var response = await CreateWith(StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), withJourney: false));

        Assert.Equal(400, response.Status);
        Assert.Contains(StrategyTemplateErrorCodes.ProductLineJourneyRequired, response.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Theory]
    [InlineData("draft", false)]
    [InlineData("published", true)]   // archived
    [InlineData("missing", false)]
    public async Task The_journey_must_be_in_the_tenant_unarchived_and_published(string state, bool archived)
    {
        var product = Guid.NewGuid();
        var journeyId = state == "missing"
            ? Guid.NewGuid()
            : _journeys.AddFor(TenantA, product,
                state == "draft" ? ContentEngagementJourneyStatuses.Draft : ContentEngagementJourneyStatuses.Published,
                archived: archived).Id;

        var response = await CreateWith(StrategyTemplateTestBuilders.ProductOnly(product, journeyId: journeyId));

        Assert.Equal(409, response.Status);
        Assert.Contains(StrategyTemplateErrorCodes.JourneyNotPublished, response.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task The_journey_must_tell_the_line_product()
    {
        var product = Guid.NewGuid();
        var otherProductsJourney = _journeys.AddFor(TenantA, Guid.NewGuid());

        var response = await CreateWith(StrategyTemplateTestBuilders.ProductOnly(product, journeyId: otherProductsJourney.Id));

        Assert.Equal(409, response.Status);
        Assert.Contains(StrategyTemplateErrorCodes.JourneyProductMismatch, response.Errors!);

        // A journey whose subject has no primary global product cannot be proven either (fail-closed).
        var orphan = _journeys.Add(TenantA);
        var unproven = await CreateWith(StrategyTemplateTestBuilders.ProductOnly(product, journeyId: orphan.Id));
        Assert.Contains(StrategyTemplateErrorCodes.JourneyProductMismatch, unproven.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task A_valid_line_stores_role_and_journey_and_the_detail_reports_them_with_the_summary()
    {
        var promoProduct = Guid.NewGuid();
        var nonPromoProduct = Guid.NewGuid();
        var nonPromoJourney = _journeys.AddFor(TenantA, nonPromoProduct);
        var created = await CreateWith(
            StrategyTemplateTestBuilders.ProductOnly(promoProduct, 10),
            StrategyTemplateTestBuilders.ProductOnly(nonPromoProduct, 20, StrategyProductLineRoles.NonPromo, nonPromoJourney.Id));
        Assert.Equal(201, created.Status);

        var stored = _templates.Stored(created.Id).ProductLines.Single(l => l.GlobalProductId == nonPromoProduct);
        Assert.Equal((StrategyProductLineRoles.NonPromo, nonPromoJourney.Id, "adoption"), (stored.Role, stored.JourneyId, stored.JourneyCodeDisplay));

        var detail = (await Detail().Handle(new GetStrategyTemplateByIdQuery(created.Id), default)).Data!;
        var line = detail.ProductLines.Single(l => l.GlobalProductId == nonPromoProduct);
        Assert.Equal((StrategyProductLineRoles.NonPromo, "adoption", "Adoption", ContentEngagementJourneyStatuses.Published, false),
            (line.Role, line.JourneyCode, line.JourneyName, line.JourneyStatus, line.JourneyMissing));
        Assert.Empty(line.JourneyWarnings!);
        Assert.Equal((1, 1, 0), (detail.PromoLineCount, detail.NonPromoLineCount, detail.LinesWithoutJourneyCount));
    }

    // ================================================================ read: language warning, old lines

    [Fact]
    public async Task A_journey_outside_the_country_scope_languages_is_a_warning_not_a_block()
    {
        var product = Guid.NewGuid();
        var english = _journeys.AddFor(TenantA, product, languageCode: "en");
        var template = Seed(new StrategyTemplateProductLine
        {
            GlobalProductId = product, Role = StrategyProductLineRoles.Promo, JourneyId = english.Id, SortOrder = 10
        });
        template.ScopeType = StrategyTemplateScopeTypes.Country;
        template.CountryScope = "TR";

        var line = Assert.Single((await Detail(new Languages()).Handle(new GetStrategyTemplateByIdQuery(template.Id), default)).Data!.ProductLines);
        Assert.Equal([StrategyProductLineJourneyWarnings.LanguageNotInCountry], line.JourneyWarnings);

        // Without a readable language set no hint is invented.
        var silent = Assert.Single((await Detail().Handle(new GetStrategyTemplateByIdQuery(template.Id), default)).Data!.ProductLines);
        Assert.Empty(silent.JourneyWarnings!);
    }

    [Fact]
    public async Task A_pre_sb3a_line_reads_as_promo_with_its_journey_missing_in_both_reads()
    {
        var template = Seed(new StrategyTemplateProductLine { GlobalProductId = Guid.NewGuid(), SortOrder = 10 });

        var detail = (await Detail().Handle(new GetStrategyTemplateByIdQuery(template.Id), default)).Data!;
        var line = Assert.Single(detail.ProductLines);
        Assert.Equal((StrategyProductLineRoles.Promo, true, (Guid?)null), (line.Role, line.JourneyMissing, line.JourneyId));
        Assert.Equal((1, 0, 1), (detail.PromoLineCount, detail.NonPromoLineCount, detail.LinesWithoutJourneyCount));

        var view = (await new GetStrategyTemplateBindingsHandler(StrategyTemplateTestDoubles.Tenant(TenantA), _templates,
                _segments, _policies, _paths, _journeys)
            .Handle(new GetStrategyTemplateBindingsQuery(template.Id, null), default)).Data!;
        var viewLine = Assert.Single(view.ProductLines);
        Assert.Equal((StrategyProductLineRoles.Promo, true), (viewLine.Role, viewLine.JourneyMissing));
        Assert.Equal(1, view.LinesWithoutJourneyCount);

        var listItem = StrategyTemplateMapper.ToListItem(_templates.Stored(template.Id));
        Assert.Equal((1, 0, 1), (listItem.PromoLineCount, listItem.NonPromoLineCount, listItem.LinesWithoutJourneyCount));
    }

    [Fact]
    public async Task A_pre_sb3a_draft_stays_renameable_but_rewritten_lines_need_role_and_journey()
    {
        var product = Guid.NewGuid();
        var template = Seed(new StrategyTemplateProductLine { GlobalProductId = product, SortOrder = 10 });
        var stored = _templates.Stored(template.Id);

        var rename = await Update().Handle(new UpdateStrategyTemplateCommand(
            template.Id, "Renamed", stored.EffectiveFrom, null, null, null, null, null, null, null, null, stored.Version), default);
        Assert.True(rename.IsSuccessful, string.Join(" ", rename.Errors ?? []));

        stored = _templates.Stored(template.Id);
        var rewrite = await Update().Handle(new UpdateStrategyTemplateCommand(
            template.Id, "Renamed", stored.EffectiveFrom, null, null, null, null, null, null,
            new[] { StrategyTemplateTestBuilders.ProductOnly(product, withJourney: false) }, null, stored.Version), default);
        Assert.Equal(400, rewrite.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ProductLineJourneyRequired, rewrite.Errors!);

        var completed = await Update().Handle(new UpdateStrategyTemplateCommand(
            template.Id, "Renamed", stored.EffectiveFrom, null, null, null, null, null, null,
            new[] { StrategyTemplateTestBuilders.ProductOnly(product) }, null, stored.Version), default);
        Assert.True(completed.IsSuccessful, string.Join(" ", completed.Errors ?? []));
        Assert.NotNull(_templates.Stored(template.Id).ProductLines.Single().JourneyId);
    }

    [Fact]
    public async Task An_active_pre_sb3a_play_is_not_changed_in_place_and_its_new_version_carries_the_lines()
    {
        var product = Guid.NewGuid();
        var template = Seed(new StrategyTemplateProductLine { GlobalProductId = product, SortOrder = 10 });
        var stored = _templates.Stored(template.Id);
        stored.TemplateStatus = StrategyTemplateStatuses.Active;
        stored.BindingsFrozenAt = StrategyTemplateTestDoubles.Now;

        var inPlace = await Update().Handle(new UpdateStrategyTemplateCommand(
            template.Id, stored.TemplateName, stored.EffectiveFrom, null, null, null, null, null, null,
            new[] { StrategyTemplateTestBuilders.ProductOnly(product) }, null, stored.Version), default);
        Assert.Equal(409, inPlace.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.BindingsFrozen, inPlace.Errors!);
        Assert.Null(_templates.Stored(template.Id).ProductLines.Single().JourneyId);

        var clone = await new CreateStrategyTemplateVersionHandler(StrategyTemplateTestDoubles.Tenant(TenantA),
            new NullActorContext(), _templates).Handle(new CreateStrategyTemplateVersionCommand(template.Id), default);
        Assert.Equal(201, clone.StatusCode);
        var draft = _templates.Stored(clone.Data);
        Assert.Equal((product, (string?)null, (Guid?)null),
            (draft.ProductLines.Single().GlobalProductId, draft.ProductLines.Single().Role, draft.ProductLines.Single().JourneyId));

        // The draft is completed by an update — the only path that writes role + journey.
        var fixedUp = await Update().Handle(new UpdateStrategyTemplateCommand(
            draft.Id, draft.TemplateName, draft.EffectiveFrom, null, null, null, null, null, null,
            new[] { StrategyTemplateTestBuilders.ProductOnly(product, role: StrategyProductLineRoles.NonPromo) }, null,
            draft.Version), default);
        Assert.True(fixedUp.IsSuccessful, string.Join(" ", fixedUp.Errors ?? []));
        Assert.Equal(StrategyProductLineRoles.NonPromo, _templates.Stored(draft.Id).ProductLines.Single().Role);
    }

    // ================================================================ retired template-level content bindings

    [Fact]
    public async Task A_new_knowledge_path_or_journey_binding_is_refused_as_retired()
    {
        var segment = _segments.Add(TenantA);
        var path = _paths.Add(TenantA);
        var journey = _journeys.Add(TenantA);
        foreach (var binding in new[] { StrategyTemplateTestBuilders.KnowledgePath(path.Id), StrategyTemplateTestBuilders.Journey(journey.Id) })
        {
            var response = await Create().Handle(
                StrategyTemplateTestBuilders.NewTemplate(segment.Id, code: "play-" + binding.ContentRefType, contentBindings: new[] { binding }),
                default);
            Assert.Equal(409, response.StatusCode);
            Assert.Contains(StrategyTemplateErrorCodes.ContentBindingTypeRetired, response.Errors!);
        }

        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task An_existing_binding_is_read_as_retired_kept_on_update_and_no_new_one_is_added()
    {
        var path = _paths.Add(TenantA);
        var template = Seed(new StrategyTemplateProductLine
        {
            GlobalProductId = Guid.NewGuid(), Role = StrategyProductLineRoles.Promo, SortOrder = 10
        });
        var stored = _templates.Stored(template.Id);
        stored.ContentBindings.Add(new StrategyTemplateContentBinding
        {
            ContentRefType = StrategyContentRefTypes.KnowledgePath, ContentRefId = path.Id, SortOrder = 10
        });

        var detail = (await Detail().Handle(new GetStrategyTemplateByIdQuery(template.Id), default)).Data!;
        Assert.True(Assert.Single(detail.ContentBindings).Retired);
        var view = (await new GetStrategyTemplateBindingsHandler(StrategyTemplateTestDoubles.Tenant(TenantA), _templates,
                _segments, _policies, _paths, _journeys)
            .Handle(new GetStrategyTemplateBindingsQuery(template.Id, null), default)).Data!;
        Assert.True(Assert.Single(view.ContentBindings).Retired);

        // Re-sending the binding it already carries is fine; adding another one is not.
        var keep = await Update().Handle(new UpdateStrategyTemplateCommand(
            template.Id, "Kept", stored.EffectiveFrom, null, null, null, null, null, null, null,
            new[] { StrategyTemplateTestBuilders.KnowledgePath(path.Id) }, stored.Version), default);
        Assert.True(keep.IsSuccessful, string.Join(" ", keep.Errors ?? []));

        stored = _templates.Stored(template.Id);
        var other = _paths.Add(TenantA);
        var add = await Update().Handle(new UpdateStrategyTemplateCommand(
            template.Id, "Kept", stored.EffectiveFrom, null, null, null, null, null, null, null,
            new[] { StrategyTemplateTestBuilders.KnowledgePath(path.Id), StrategyTemplateTestBuilders.KnowledgePath(other.Id, 20) },
            stored.Version), default);
        Assert.Equal(409, add.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ContentBindingTypeRetired, add.Errors!);
    }

    // ================================================================ class map

    [Fact]
    public void Role_and_journey_round_trip_as_strings_and_a_pre_sb3a_line_still_reads()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var journeyId = Guid.NewGuid();
        var line = new StrategyTemplateProductLine
        {
            GlobalProductId = Guid.NewGuid(), Role = StrategyProductLineRoles.NonPromo, JourneyId = journeyId, JourneyCodeDisplay = "J-1"
        };
        var doc = line.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["JourneyId"].BsonType);
        var back = BsonSerializer.Deserialize<StrategyTemplateProductLine>(doc);
        Assert.Equal((StrategyProductLineRoles.NonPromo, (Guid?)journeyId, "J-1"), (back.Role, back.JourneyId, back.JourneyCodeDisplay));

        doc.Remove("Role");
        doc.Remove("JourneyId");
        doc.Remove("JourneyCodeDisplay");
        var legacy = BsonSerializer.Deserialize<StrategyTemplateProductLine>(doc);
        Assert.Equal((StrategyProductLineRoles.Promo, (Guid?)null), (legacy.EffectiveRole(), legacy.JourneyId));
    }

    // ================================================================ helpers

    /// <summary>A stored draft (as an older writer left it) — seeded directly, since the write path would refuse it.</summary>
    private TemplateEntity Seed(StrategyTemplateProductLine line)
    {
        var segment = _segments.Add(TenantA);
        var id = Guid.NewGuid();
        var template = new TemplateEntity
        {
            Id = id, TenantId = TenantA, TemplateCode = "legacy-" + id.ToString("N")[..6], TemplateName = "Legacy play",
            SubjectType = StrategyTemplateSubjectTypes.Contact, TemplateStatus = StrategyTemplateStatuses.Draft,
            TemplateVersion = 1, VersionLineageId = id, EffectiveFrom = StrategyTemplateTestDoubles.Past,
            SegmentBindings = { new StrategyTemplateSegmentBinding { SegmentId = segment.Id, SortOrder = 10 } },
            FrequencyIntent = new StrategyTemplateFrequencyIntent { Mode = StrategyFrequencyIntentModes.None },
            ProductLines = { line }
        };
        _templates.Rows.Add(template);
        return template;
    }

    /// <summary>BRD country-content-languages: TR speaks tr.</summary>
    private sealed class Languages : IReferenceDataCatalogReader
    {
        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
            => Task.FromResult(new ReferenceSetSnapshot(setCode, true, new[]
            {
                new ReferenceValueSnapshot("TR", "TR", null, true, false, new Dictionary<string, string> { ["Languages"] = "tr" })
            }));
    }
}
