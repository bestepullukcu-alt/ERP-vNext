using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Application.Features.ContentComposition.ContentSets;
using Diten.CrmService.Application.Features.ContentComposition.Eligibility;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-SB-1R (bridge-decision §7) — the ContentScope is retired; a content set carries its country + language and
/// derives product + audience from its composition template. Pins: country / language validation against BRD
/// (country_invalid, language_not_in_country, 503 when BRD is unreadable), the single-language rule on component add and
/// on a language change (409 component_language_mismatch), draft-only context change (409 context_locked), the context
/// resolver (primary global-product link, for-whom profiles) on the set read, the eligibility context built from the set,
/// the context frozen into a revision, and the class map (legacy "Scope" element still reads, new fields round-trip).
/// </summary>
public sealed class ContentSetContextTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ================================================================ country / language validation

    [Fact]
    public async Task Create_stores_the_country_upper_and_the_language_lower()
    {
        var fx = new Fixture();

        var r = await fx.Create().Handle(fx.CreateCmd(" tr ", "TR"), default);

        Assert.Equal(201, r.StatusCode);
        var set = fx.Sets.Items.Single();
        Assert.Equal("TR", set.CountryCode);
        Assert.Equal("tr", set.LanguageCode);
    }

    [Theory]
    [InlineData("XX", "tr", ContentSetContextErrors.CountryInvalid)]       // not in COUNTRY_CODES
    [InlineData("", "tr", ContentSetContextErrors.CountryInvalid)]         // missing
    [InlineData("TR", "en", ContentSetContextErrors.LanguageNotInCountry)] // not a TR content language
    [InlineData("TR", "", ContentSetContextErrors.LanguageNotInCountry)]   // missing
    [InlineData("QQ", "qq", ContentSetContextErrors.LanguageNotInCountry)] // a country with no languages configured
    public async Task Create_rejects_an_invalid_country_or_language_with_400(string country, string language, string code)
    {
        var fx = new Fixture();

        var r = await fx.Create().Handle(fx.CreateCmd(country, language), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal(code, r.Errors![0]);
        Assert.Empty(fx.Sets.Items);
    }

    [Fact]
    public async Task Create_is_503_when_the_reference_data_cannot_be_read()
    {
        var fx = new Fixture();
        fx.Catalog.Published = false;

        var r = await fx.Create().Handle(fx.CreateCmd("TR", "tr"), default);

        Assert.Equal(503, r.StatusCode);
        Assert.Equal(ContentSetContextErrors.ReferenceSetUnavailable, r.Errors![0]);
        Assert.Empty(fx.Sets.Items);
    }

    [Fact]
    public async Task Create_is_503_without_a_catalog_reader_never_a_local_list()
    {
        var fx = new Fixture();
        var handler = new CreateContentSetDraftHandler(
            Tenant(), new NullActorContext(), fx.Sets, fx.Templates, catalog: null);

        var r = await handler.Handle(fx.CreateCmd("TR", "tr"), default);

        Assert.Equal(503, r.StatusCode);
    }

    // ================================================================ single-language rule

    [Fact]
    public async Task A_component_in_another_language_is_409_component_language_mismatch()
    {
        var fx = new Fixture();
        var setId = await fx.CreatedSetAsync("TR", "tr");
        var english = fx.SeedContent("KC-EN", "en");
        var turkish = fx.SeedContent("KC-TR", "tr");

        var refused = await fx.AddComponent().Handle(
            new AddContentSetComponentCommand(setId, english.Id, fx.StepType, 0, "B1"), default);
        var accepted = await fx.AddComponent().Handle(
            new AddContentSetComponentCommand(setId, turkish.Id, fx.StepType, 0, "B1"), default);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(ContentSetContextErrors.ComponentLanguageMismatch, refused.Errors![0]);
        Assert.Contains("KC-EN", refused.Errors[1]);
        Assert.Equal(201, accepted.StatusCode);
        Assert.Single(fx.Sets.Items.Single().SelectedComponents);
    }

    [Fact]
    public async Task Changing_the_language_while_components_do_not_match_is_409_and_nothing_changes()
    {
        var fx = new Fixture();
        var setId = await fx.CreatedSetAsync("UZ", "uz");
        var uzbek = fx.SeedContent("KC-UZ", "uz");
        await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, uzbek.Id, fx.StepType, 0, "B1"), default);

        var r = await fx.Update().Handle(new UpdateContentSetCommand(setId, "Set", LanguageCode: "ru"), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ContentSetContextErrors.ComponentLanguageMismatch, r.Errors![0]);
        Assert.Equal("uz", fx.Sets.Items.Single().LanguageCode);
    }

    [Fact]
    public async Task A_draft_changes_country_and_language_and_null_keeps_them()
    {
        var fx = new Fixture();
        var setId = await fx.CreatedSetAsync("TR", "tr");

        Assert.Equal(200, (await fx.Update().Handle(new UpdateContentSetCommand(setId, "Set", CountryCode: "UZ", LanguageCode: "ru"), default)).StatusCode);
        Assert.Equal(200, (await fx.Update().Handle(new UpdateContentSetCommand(setId, "Renamed"), default)).StatusCode);

        var set = fx.Sets.Items.Single();
        Assert.Equal("UZ", set.CountryCode);
        Assert.Equal("ru", set.LanguageCode);
        Assert.Equal("Renamed", set.SetName);

        var invalid = await fx.Update().Handle(new UpdateContentSetCommand(setId, "Set", LanguageCode: "tr"), default);
        Assert.Equal(400, invalid.StatusCode);
        Assert.Equal(ContentSetContextErrors.LanguageNotInCountry, invalid.Errors![0]);
    }

    [Fact]
    public async Task The_context_of_a_set_that_is_no_longer_a_draft_is_locked()
    {
        var fx = new Fixture();
        var setId = await fx.CreatedSetAsync("TR", "tr");
        fx.Sets.Items.Single().Status = ContentSetStatuses.Inactive;

        var r = await fx.Update().Handle(new UpdateContentSetCommand(setId, "Set", CountryCode: "UZ", LanguageCode: "uz"), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ContentSetContextErrors.ContextLocked, r.Errors![0]);
    }

    // ================================================================ derived context

    [Fact]
    public async Task The_set_read_carries_country_language_and_the_template_derived_product_and_audience()
    {
        var fx = new Fixture();
        var setId = await fx.CreatedSetAsync("TR", "tr");

        var dto = (await new GetContentSetHandler(Tenant(), fx.Sets, fx.Resolver()).Handle(new GetContentSetQuery(setId), default)).Data!;

        Assert.Equal("TR", dto.CountryCode);
        Assert.Equal("tr", dto.LanguageCode);
        var context = dto.Context!;
        Assert.Equal(ProductX, context.ProductId);                  // the PRIMARY global-product link (not the other one)
        Assert.Equal("ALMIBA", context.ProductCode);
        Assert.Equal("Almiba 1 g", context.ProductName);
        var audience = Assert.Single(context.Audiences);
        Assert.Equal(fx.Profile.Id, audience.AudienceProfileId);
        Assert.Equal("AUDP-NEF", audience.ProfileCode);
        Assert.Equal("Nefroloji", audience.ProfileName);
        Assert.Equal(new[] { fx.Profile.Id }, context.AudienceProfileIds);
    }

    [Fact]
    public async Task The_eligibility_context_is_built_from_the_set_not_from_a_scope()
    {
        var fx = new Fixture();
        var setId = await fx.CreatedSetAsync("TR", "tr");
        var claim = new Claim
        {
            TenantId = TenantA, ClaimCode = "CLM-1", ClaimName = "C", ClaimText = "t", ClaimVersion = "1.0",
            Status = ClaimStatuses.Approved, EffectiveFrom = Jan1,
            Applicability = new ClaimApplicability { EligibilityPolicyId = Guid.NewGuid() }
        };
        fx.Claims.Items.Add(claim);
        fx.Sets.Items.Single().SelectedClaims.Add(new ContentSetClaim { ClaimId = claim.Id, ClaimVersion = "1.0" });
        var turkish = fx.SeedContent("KC-TR", "tr");
        await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, turkish.Id, fx.StepType, 0, "B1"), default);

        var r = await new ApplyContentSetEligibilityHandler(Tenant(), new NullActorContext(), fx.Sets, fx.Resolver(),
            fx.Claims, fx.Port).Handle(new ApplyContentSetEligibilityCommand(setId), default);

        Assert.Equal(200, r.StatusCode);
        var dims = fx.Port.LastContext!.Dimensions.ToDictionary(d => d.Dimension, d => d.Values);
        Assert.Equal(new[] { "ALMIBA" }, dims["product"]);       // MDM product code
        Assert.Equal(new[] { "TR" }, dims["market"]);            // set country
        Assert.Equal(new[] { "AUDP-NEF" }, dims["audience"]);    // for-whom profile code
        Assert.Equal(new[] { "tr" }, dims["language"]);
        Assert.False(dims.ContainsKey("channel"));
    }

    [Fact]
    public async Task Submit_freezes_the_set_context_into_the_revision()
    {
        var fx = new Fixture();
        var setId = await fx.CreatedSetAsync("TR", "tr");
        var turkish = fx.SeedContent("KC-TR", "tr");
        await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, turkish.Id, fx.StepType, 0, "B1"), default);

        var r = await new SubmitContentSetForReviewHandler(Tenant(), new NullActorContext(), fx.Sets, fx.Revisions,
            fx.Resolver()).Handle(new SubmitContentSetForReviewCommand(setId), default);

        Assert.True(r.IsSuccessful, string.Join(" ", r.Errors ?? Array.Empty<string>()));
        var context = fx.Revisions.Items.Single().Context!;
        Assert.Equal("TR", context.CountryCode);
        Assert.Equal("tr", context.LanguageCode);
        Assert.Equal(ProductX, context.ProductId);
        Assert.Equal("ALMIBA", context.ProductCode);
        Assert.Equal(new[] { fx.Profile.Id }, context.AudienceProfileIds);
        Assert.Equal("TR", ContentSetRevisionMapper.ToDto(fx.Revisions.Items.Single()).Context!.CountryCode);
    }

    // ================================================================ class map

    [Fact]
    public void A_pre_sb1r_set_and_revision_with_a_scope_element_still_read_and_the_new_fields_round_trip()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var legacyScope = new BsonDocument
        {
            { "ContentScopeId", Guid.NewGuid().ToString() },
            { "ScopeVersion", "1.0" }
        };

        var setDoc = new ContentSet { TenantId = TenantA, SetCode = "SET-OLD", SetName = "Old" }.ToBsonDocument();
        setDoc.Remove("CountryCode");
        setDoc.Remove("LanguageCode");
        setDoc["Scope"] = legacyScope;
        var set = BsonSerializer.Deserialize<ContentSet>(setDoc);   // must not throw on the retired element
        Assert.Equal("SET-OLD", set.SetCode);
        Assert.Null(set.CountryCode);

        var revisionDoc = new ContentSetRevision { TenantId = TenantA, RevisionCode = "SET-OLD-R1" }.ToBsonDocument();
        revisionDoc.Remove("Context");
        revisionDoc["Scope"] = legacyScope.DeepClone();
        var revision = BsonSerializer.Deserialize<ContentSetRevision>(revisionDoc);
        Assert.Equal("SET-OLD-R1", revision.RevisionCode);
        Assert.Null(revision.Context);
#pragma warning disable CS0618 // the legacy element is kept for read-compatibility only
        Assert.Equal("1.0", revision.LegacyScope!.ScopeVersion);
        Assert.Equal("1.0", set.LegacyScope!.ScopeVersion);
#pragma warning restore CS0618
        Assert.True(revision.ToBsonDocument().Contains("Scope"));  // a re-save keeps the frozen legacy element

        var fresh = new ContentSetRevision
        {
            TenantId = TenantA,
            Context = new ContentSetContextSnapshot
            {
                CountryCode = "TR", LanguageCode = "tr", ProductId = ProductX, ProductCode = "ALMIBA",
                AudienceProfileIds = { ProductX }
            }
        };
        var freshDoc = fresh.ToBsonDocument();
        Assert.False(freshDoc.Contains("Scope"));                  // nothing new writes the retired element
        Assert.Equal(BsonType.String, freshDoc["Context"]["ProductId"].BsonType);
        Assert.Equal(BsonType.String, freshDoc["Context"]["AudienceProfileIds"].AsBsonArray[0].BsonType);
        Assert.Equal("ALMIBA", BsonSerializer.Deserialize<ContentSetRevision>(freshDoc).Context!.ProductCode);

        var newSet = new ContentSet { TenantId = TenantA, CountryCode = "TR", LanguageCode = "tr" }.ToBsonDocument();
        Assert.False(newSet.Contains("Scope"));
        Assert.Equal("TR", BsonSerializer.Deserialize<ContentSet>(newSet).CountryCode);
    }

    // ================================================================ fixture

    private static TenantContext Tenant()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(TenantA);
        return ctx;
    }

    private sealed class Fixture
    {
        public ContentSetTestSets Sets { get; } = new();
        public ContentSetTestTemplates Templates { get; } = new();
        public ContentSetTestContents Contents { get; } = new();
        public ContentSetTestCatalog Catalog { get; } = new();
        public ContentSetTestSubjects Subjects { get; } = new();
        public ContentSetTestProfiles Profiles { get; } = new();
        public ContentSetTestClaims Claims { get; } = new();
        public ContentSetTestRevisions Revisions { get; } = new();
        public CapturingPort Port { get; } = new();
        public Guid StepType { get; } = Guid.NewGuid();
        public ConceptChainTemplate Template { get; }
        public AudienceProfile Profile { get; }

        public Fixture()
        {
            var subject = new Subject
            {
                TenantId = TenantA, SubjectCode = "ALM", SubjectName = "Almiba",
                ExternalReferences =
                {
                    new KnowledgeExternalReference { SourceSystem = "global-product", ExternalId = Guid.NewGuid().ToString(), ExternalCode = "OTHER" },
                    new KnowledgeExternalReference
                    {
                        SourceSystem = "global-product", ExternalId = ProductX.ToString(), ExternalCode = "ALMIBA",
                        ExternalName = "Almiba 1 g", IsPrimary = true
                    }
                }
            };
            Subjects.Items.Add(subject);
            Profile = new AudienceProfile { TenantId = TenantA, ProfileCode = "AUDP-NEF", ProfileName = "Nefroloji" };
            Profiles.Items.Add(Profile);
            Template = new ConceptChainTemplate
            {
                TenantId = TenantA, ChainCode = "TPL-ALM", ChainName = "Almiba", SubjectId = subject.Id, ChainVersion = "1.0",
                Status = ConceptChainStatuses.Published, EffectiveFrom = Jan1,
                OrderedConceptTypes = { StepType, Guid.NewGuid() },
                ForWhomAudienceProfileIds = { Profile.Id },
                Branches = { new ConceptChainBranch { BranchCode = "B1", Steps = { new ConceptChainStep { ConceptTypeId = StepType } } } }
            };
            Templates.Items.Add(Template);
        }

        public CreateContentSetDraftCommand CreateCmd(string country, string language)
            => new("SET-" + Guid.NewGuid().ToString("N")[..6], "Set", Template.Id, CountryCode: country, LanguageCode: language);

        public CreateContentSetDraftHandler Create() => new(Tenant(), new NullActorContext(), Sets, Templates, Catalog);
        public UpdateContentSetHandler Update() => new(Tenant(), new NullActorContext(), Sets, catalog: Catalog);
        public AddContentSetComponentHandler AddComponent() => new(Tenant(), new NullActorContext(), Sets, Templates, Contents);
        public ContentSetContextResolver Resolver() => new(Templates, Subjects, Profiles);

        public async Task<Guid> CreatedSetAsync(string country, string language)
        {
            var r = await Create().Handle(CreateCmd(country, language), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public KnowledgeContent SeedContent(string code, string language)
        {
            var content = new KnowledgeContent
            {
                TenantId = TenantA, ContentCode = code, ContentTitle = code, ContentType = KnowledgeContentTypes.Presentation,
                ContentStatus = KnowledgeContentStatuses.Published, SubjectId = Guid.NewGuid(), LanguageCode = language,
                ContentVersion = "1.0", EffectiveFrom = Jan1, Url = "https://x"
            };
            Contents.Items.Add(content);
            return content;
        }
    }

    private sealed class CapturingPort : IEligibilityEvaluationPort
    {
        public EligibilityContext? LastContext { get; private set; }

        public Task<Response<EligibilityResult>> EvaluateAsync(ResolveEligibilityQuery query, CancellationToken ct)
        {
            LastContext = query.Context;
            return Task.FromResult(Response<EligibilityResult>.Success(new EligibilityResult(
                EligibilityState.Eligible, null, null, query.PolicyId, "1.0", Array.Empty<EligibilityConditionOutcome>(),
                DateTimeOffset.UtcNow)));
        }
    }
}

// ---------------- shared WP-SB-1R test doubles (also used by ContentSetAssemblyTests) ----------------

/// <summary>BRD reference data for the set context: COUNTRY_CODES (TR, UZ, GB, QQ) and country-content-languages
/// (TR: tr · UZ: uz, ru · GB: en · QQ: none). <see cref="Published"/> false = unreadable.</summary>
internal sealed class ContentSetTestCatalog : IReferenceDataCatalogReader
{
    public bool Published { get; set; } = true;

    public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
    {
        if (!Published)
        {
            return Task.FromResult(ReferenceSetSnapshot.NotPublished(setCode));
        }

        IReadOnlyList<ReferenceValueSnapshot> values = setCode switch
        {
            "COUNTRY_CODES" => new[] { "TR", "UZ", "GB", "QQ" }
                .Select(c => new ReferenceValueSnapshot(c, c, null, true, false, null)).ToList(),
            "country-content-languages" => new[]
            {
                Row("TR", "tr"), Row("UZ", "uz,ru"), Row("GB", "en"),
                new ReferenceValueSnapshot("QQ", "QQ", null, true, false, null)
            },
            _ => Array.Empty<ReferenceValueSnapshot>()
        };
        return Task.FromResult(new ReferenceSetSnapshot(setCode, true, values));
    }

    private static ReferenceValueSnapshot Row(string country, string languages)
        => new(country, country, null, true, false, new Dictionary<string, string> { ["Languages"] = languages });
}

internal sealed class ContentSetTestSubjects : ISubjectRepository
{
    public List<Subject> Items { get; } = new();
    public Task<Subject?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == t && s.Id == id));
    public Task<IReadOnlyList<Subject>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<Subject>)Items.Where(s => s.TenantId == t).ToList());
    public Task<Subject?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<Subject?>(null);
    public Task InsertAsync(Subject subject, CancellationToken ct) { Items.Add(subject); return Task.CompletedTask; }
    public Task UpdateAsync(Subject subject, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestProfiles : IAudienceProfileRepository
{
    public List<AudienceProfile> Items { get; } = new();
    public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id));
    public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<AudienceProfile>)Items.Where(p => p.TenantId == t).ToList());
    public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<AudienceProfile?>(null);
    public Task InsertAsync(AudienceProfile profile, CancellationToken ct) { Items.Add(profile); return Task.CompletedTask; }
    public Task UpdateAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestSets : IContentSetRepository
{
    public List<ContentSet> Items { get; } = new();
    public Task<ContentSet?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
    public Task<IReadOnlyList<ContentSet>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ContentSet>)Items.Where(x => x.TenantId == t).ToList());
    public Task<ContentSet?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.SetCode == code && !x.IsArchived()));
    public Task InsertAsync(ContentSet entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
    public Task UpdateAsync(ContentSet entity, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestTemplates : IConceptChainTemplateRepository
{
    public List<ConceptChainTemplate> Items { get; } = new();
    public Task<ConceptChainTemplate?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
    public Task<IReadOnlyList<ConceptChainTemplate>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t).ToList());
    public Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && x.SubjectId == s).ToList());
    public Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && x.ChainCode == code).ToList());
    public Task InsertAsync(ConceptChainTemplate entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
    public Task UpdateAsync(ConceptChainTemplate entity, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestContents : IKnowledgeContentRepository
{
    public List<KnowledgeContent> Items { get; } = new();
    public Task<KnowledgeContent?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));
    public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<KnowledgeContent>)Items.Where(c => c.TenantId == t).ToList());
    public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ContentCode == code && !c.IsArchived()));
    public Task InsertAsync(KnowledgeContent content, CancellationToken ct) { Items.Add(content); return Task.CompletedTask; }
    public Task UpdateAsync(KnowledgeContent content, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestClaims : IClaimRepository
{
    public List<Claim> Items { get; } = new();
    public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));
    public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t).ToList());
    public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t && c.ClaimCode == code).ToList());
    public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ClaimCode == code && !c.IsArchived()));
    public Task InsertAsync(Claim entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
    public Task UpdateAsync(Claim entity, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestRevisions : IContentSetRevisionRepository
{
    public List<ContentSetRevision> Items { get; } = new();
    public Task<ContentSetRevision?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
    public Task<IReadOnlyList<ContentSetRevision>> ListByContentSetAsync(Guid t, Guid setId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ContentSetRevision>)Items.Where(x => x.TenantId == t && x.ContentSetId == setId).ToList());
    public Task InsertAsync(ContentSetRevision e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
    public Task UpdateAsync(ContentSetRevision e, CancellationToken ct) => Task.CompletedTask;
}
