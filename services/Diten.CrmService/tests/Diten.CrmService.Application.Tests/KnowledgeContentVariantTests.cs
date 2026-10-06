using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Knowledge.Concept;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Application.Features.Knowledge.Content.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Content.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using ContentEntity = Diten.CrmService.Domain.Entities.KnowledgeContent;
using SubjectEntity = Diten.CrmService.Domain.Entities.Subject;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-13 (docx §13) — language-variant tracking on MOD-0162 KnowledgeContent. Pins down: a target variant shares its
/// source's logical component (ContentSetId) in a different language; the read-time migration turns a pre-variant row
/// into its own single-language source; a content-affecting source edit opens translation assessment on its targets
/// while a metadata-only edit does not; resolution never falls back to another language (explicit Unresolved);
/// mark-assessed clears the flag; the one-active-variant-per-language guard holds; and MOD-0162 audit fires.
/// </summary>
public sealed class KnowledgeContentVariantTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeContentRepo Contents { get; } = new();
        public FakeSubjectRepo Subjects { get; } = new();
        public FakeTopicRepo Topics { get; } = new();
        public FakeAudienceProfileRepo Profiles { get; } = new();
        public FakeConceptNodeRepo ConceptNodes { get; } = new();
        public CapturingAudit Audit { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateKnowledgeContentHandler CreateContent()
            => new(Tenant(TenantId), new NullActorContext(), Contents, Subjects, Topics, Profiles, ConceptNodes);

        public UpdateKnowledgeContentHandler UpdateContent()
            => new(Tenant(TenantId), new NullActorContext(), Contents, Subjects, Topics, Profiles, ConceptNodes, Audit);

        public CreateContentVariantHandler CreateVariant()
            => new(Tenant(TenantId), new NullActorContext(), Contents, Audit);

        public MarkTranslationAssessedHandler MarkAssessed()
            => new(Tenant(TenantId), new NullActorContext(), Contents, Audit);

        public GetKnowledgeContentHandler GetContent(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Contents);

        public KnowledgeContentLinkageReader Reader(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Contents);

        public Guid SeedSubject(string code = "SUB-1")
        {
            var s = new SubjectEntity
            {
                TenantId = TenantId, SubjectCode = code, SubjectName = "Subject " + code,
                Status = "active", EffectiveFrom = Jan1, CreatedAt = Jan1
            };
            Subjects.Items.Add(s);
            return s.Id;
        }

        // Inserts a fully-formed variant directly (bypassing the create path) for set-composition tests.
        public ContentEntity SeedContent(
            Guid subjectId, Guid setId, bool isSource, string language, string code,
            string status = KnowledgeContentStatuses.Published, string? url = "https://example.test/x",
            string? summary = null, string translationStatus = ContentTranslationStatuses.Current, bool archived = false)
        {
            var c = new ContentEntity
            {
                TenantId = TenantId, ContentCode = code, ContentTitle = "Title " + code,
                ContentType = KnowledgeContentTypes.Presentation, ContentStatus = archived ? KnowledgeContentStatuses.Archived : status,
                SubjectId = subjectId, LanguageCode = language, ContentVersion = "1.0", EffectiveFrom = Jan1,
                Source = KnowledgeContentSources.Manual, Url = url, Summary = summary,
                ContentSetId = setId, IsSourceLanguage = isSource, TranslationStatus = translationStatus,
                CreatedAt = Jan1
            };
            if (archived) { c.ArchivedAt = Jan1; }
            Contents.Items.Add(c);
            return c;
        }
    }

    private static UpdateKnowledgeContentCommand UpdateCmd(
        ContentEntity c, string? title = null, string? summary = null, string? url = null)
        => new(c.Id, title ?? c.ContentTitle, c.ContentType, c.SubjectId, c.LanguageCode, c.ContentVersion,
            c.EffectiveFrom, KnowledgeContentStatuses.Published, Summary: summary ?? c.Summary,
            Url: url ?? c.Url);

    // 1 — create source (plain create) is its own single-language component (self-source, current).
    [Fact]
    public async Task Plain_create_content_is_its_own_source()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var id = (await fx.CreateContent().Handle(
            new CreateKnowledgeContentCommand("KC-SRC", "Deck", KnowledgeContentTypes.Presentation, subjectId, "en",
                "1.0", Jan1, KnowledgeContentStatuses.Published, Url: "https://example.test/d"), default)).Data;

        var dto = (await fx.GetContent().Handle(new GetKnowledgeContentQuery(id), default)).Data!;
        Assert.Equal(id, dto.ContentSetId);   // set == its own id
        Assert.True(dto.IsSourceLanguage);
        Assert.Equal(ContentTranslationStatuses.Current, dto.TranslationStatus);
    }

    // 2 — create target variant: shares the source's ContentSetId, different language, not source, current.
    [Fact]
    public async Task Create_variant_shares_set_and_is_a_current_target()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id; // a clean self-source

        var created = await fx.CreateVariant().Handle(new CreateContentVariantCommand(
            source.Id, "tr", "KC-TR", "Türkçe", "1.0", Jan1, KnowledgeContentStatuses.Draft,
            Url: "https://example.test/tr"), default);

        Assert.Equal(201, created.StatusCode);
        var dto = (await fx.GetContent().Handle(new GetKnowledgeContentQuery(created.Data), default)).Data!;
        Assert.Equal(source.ContentSetId, dto.ContentSetId);
        Assert.False(dto.IsSourceLanguage);
        Assert.Equal("tr", dto.LanguageCode);
        Assert.Equal(subjectId, dto.SubjectId);       // classification inherited from source
        Assert.Equal(ContentTranslationStatuses.Current, dto.TranslationStatus);
    }

    // 3 — read-time migration: a legacy row (no ContentSetId) reads back as its own source.
    [Fact]
    public async Task Legacy_row_migrates_to_self_source_on_read()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var legacy = new ContentEntity
        {
            TenantId = TenantA, ContentCode = "KC-LEG", ContentTitle = "Legacy",
            ContentType = KnowledgeContentTypes.Presentation, ContentStatus = KnowledgeContentStatuses.Published,
            SubjectId = subjectId, LanguageCode = "en", ContentVersion = "1.0", EffectiveFrom = Jan1,
            Source = KnowledgeContentSources.Manual, Url = "https://example.test/legacy", CreatedAt = Jan1
            // ContentSetId deliberately unset (Guid.Empty), TranslationStatus unset
        };
        fx.Contents.Items.Add(legacy);

        var dto = (await fx.GetContent().Handle(new GetKnowledgeContentQuery(legacy.Id), default)).Data!;
        Assert.Equal(legacy.Id, dto.ContentSetId);
        Assert.True(dto.IsSourceLanguage);
        Assert.Equal(ContentTranslationStatuses.Current, dto.TranslationStatus);
    }

    // 4 — source content edit opens translation assessment on its targets.
    [Fact]
    public async Task Source_body_edit_opens_translation_assessment_on_targets()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var setId = Guid.NewGuid();
        var source = fx.SeedContent(subjectId, setId, isSource: true, language: "en", code: "KC-EN", summary: "old");
        source.ContentSetId = source.Id; setId = source.Id;
        var target = fx.SeedContent(subjectId, setId, isSource: false, language: "tr", code: "KC-TR");

        var r = await fx.UpdateContent().Handle(UpdateCmd(source, summary: "NEW BODY"), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(ContentTranslationStatuses.NeedsAssessment, target.TranslationStatus);
        Assert.Contains(fx.Audit.Events, e =>
            e.Event == KnowledgeReasonCodes.ContentTranslationAssessmentOpened && e.EntityId == target.Id);
    }

    // 5 — metadata-only source edit (title rename) does NOT trigger assessment.
    [Fact]
    public async Task Metadata_only_source_edit_does_not_open_assessment()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN", summary: "same");
        source.ContentSetId = source.Id;
        var target = fx.SeedContent(subjectId, source.Id, isSource: false, language: "tr", code: "KC-TR");

        var r = await fx.UpdateContent().Handle(UpdateCmd(source, title: "Renamed Title"), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(ContentTranslationStatuses.Current, target.TranslationStatus);   // untouched
    }

    // 6 — no silent fallback: resolving a language with no variant returns Unresolved, never another language.
    [Fact]
    public async Task Resolve_missing_language_is_unresolved_never_a_fallback()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id;
        fx.SeedContent(subjectId, source.Id, isSource: false, language: "tr", code: "KC-TR");

        var fr = await fx.Reader().ResolveVariantAsync(source.ContentSetId, "fr", default);
        Assert.Equal(KnowledgeContentVariantResolutionStatus.Unresolved, fr.Status);
        Assert.Null(fr.Content);   // never substitutes en or tr

        var tr = await fx.Reader().ResolveVariantAsync(source.ContentSetId, "tr", default);
        Assert.Equal(KnowledgeContentVariantResolutionStatus.Resolved, tr.Status);
        Assert.Equal("tr", tr.Content!.LanguageCode);
    }

    // 7 — mark-assessed clears needs_assessment → current (and audits).
    [Fact]
    public async Task Mark_assessed_clears_needs_assessment()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id;
        var target = fx.SeedContent(subjectId, source.Id, isSource: false, language: "tr", code: "KC-TR",
            translationStatus: ContentTranslationStatuses.NeedsAssessment);

        var r = await fx.MarkAssessed().Handle(new MarkTranslationAssessedCommand(target.Id), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(ContentTranslationStatuses.Current, target.TranslationStatus);
        Assert.Contains(fx.Audit.Events, e =>
            e.Event == KnowledgeReasonCodes.ContentTranslationAssessed && e.EntityId == target.Id);
    }

    // 8 — mark-assessed on the source is rejected (source has no translation status).
    [Fact]
    public async Task Mark_assessed_on_source_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id;

        var r = await fx.MarkAssessed().Handle(new MarkTranslationAssessedCommand(source.Id), default);
        Assert.Equal(400, r.StatusCode);
    }

    // 9 — one active variant per language: a second variant for the same language is rejected.
    [Fact]
    public async Task Second_variant_for_same_language_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id;
        fx.SeedContent(subjectId, source.Id, isSource: false, language: "tr", code: "KC-TR");

        var r = await fx.CreateVariant().Handle(new CreateContentVariantCommand(
            source.Id, "tr", "KC-TR2", "Türkçe 2", "1.0", Jan1, Url: "https://example.test/tr2"), default);
        Assert.Equal(409, r.StatusCode);
    }

    // 10 — a variant cannot reuse the source's own language (single active per language covers the source too).
    [Fact]
    public async Task Variant_in_source_language_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id;

        var r = await fx.CreateVariant().Handle(new CreateContentVariantCommand(
            source.Id, "en", "KC-EN2", "English 2", "1.0", Jan1, Url: "https://example.test/en2"), default);
        Assert.Equal(409, r.StatusCode);
    }

    // 11 — a variant cannot be added to an archived source.
    [Fact]
    public async Task Variant_on_archived_source_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN", archived: true);
        source.ContentSetId = source.Id;

        var r = await fx.CreateVariant().Handle(new CreateContentVariantCommand(
            source.Id, "tr", "KC-TR", "Türkçe", "1.0", Jan1, Url: "https://example.test/tr"), default);
        Assert.Equal(409, r.StatusCode);
    }

    // 12 — variant create emits a MOD-0162 audit event.
    [Fact]
    public async Task Variant_create_emits_mod0162_audit()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id;

        var created = await fx.CreateVariant().Handle(new CreateContentVariantCommand(
            source.Id, "tr", "KC-TR", "Türkçe", "1.0", Jan1, Url: "https://example.test/tr"), default);

        var evt = Assert.Single(fx.Audit.Events, e => e.Event == KnowledgeReasonCodes.ContentVariantCreated);
        Assert.Equal(KnowledgeConceptAuditEntities.KnowledgeContent, evt.EntityType);
        Assert.Equal(created.Data, evt.EntityId);
    }

    // 13 — cross-tenant isolation: a variant resolution in another tenant sees nothing.
    [Fact]
    public async Task Resolve_variant_is_tenant_isolated()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var source = fx.SeedContent(subjectId, Guid.NewGuid(), isSource: true, language: "en", code: "KC-EN");
        source.ContentSetId = source.Id;

        var other = await fx.Reader(TenantB).ResolveVariantAsync(source.ContentSetId, "en", default);
        Assert.Equal(KnowledgeContentVariantResolutionStatus.Unresolved, other.Status);
    }

    // ---------------- in-memory fakes (mirror the real repo: reads apply EnsureVariantDefaults) ----------------

    private sealed class CapturingAudit : IKnowledgeConceptAuditPublisher
    {
        public List<(string Event, string EntityType, Guid EntityId)> Events { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version,
            string? detail, CancellationToken cancellationToken)
        {
            Events.Add((eventName, entityType, entityId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeContentRepo : IKnowledgeContentRepository
    {
        public List<ContentEntity> Items { get; } = new();

        public Task<ContentEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        {
            var row = Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted);
            row?.EnsureVariantDefaults();   // read-time migration, exactly like KnowledgeContentRepository
            return Task.FromResult(row);
        }

        public Task<IReadOnlyList<ContentEntity>> ListAsync(Guid t, CancellationToken ct)
        {
            var rows = Items.Where(c => c.TenantId == t && !c.IsDeleted).ToList();
            foreach (var row in rows) { row.EnsureVariantDefaults(); }
            return Task.FromResult((IReadOnlyList<ContentEntity>)rows.OrderByDescending(c => c.CreatedAt).ToList());
        }

        public Task<ContentEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c =>
                c.TenantId == t && !c.IsDeleted && c.ContentCode == code && !c.IsArchived()));

        public Task InsertAsync(ContentEntity content, CancellationToken ct)
        {
            Items.Add(content);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ContentEntity content, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSubjectRepo : ISubjectRepository
    {
        public List<SubjectEntity> Items { get; } = new();

        public Task<SubjectEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == t && s.Id == id && !s.IsDeleted));

        public Task<IReadOnlyList<SubjectEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SubjectEntity>)Items.Where(s => s.TenantId == t && !s.IsDeleted).ToList());

        public Task<SubjectEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s =>
                s.TenantId == t && !s.IsDeleted && s.SubjectCode == code && !s.IsArchived()));

        public Task InsertAsync(SubjectEntity subject, CancellationToken ct) { Items.Add(subject); return Task.CompletedTask; }
        public Task UpdateAsync(SubjectEntity subject, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTopicRepo : ITopicRepository
    {
        public List<Topic> Items { get; } = new();

        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == subjectId).ToList());

        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid subjectId, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.SubjectId == subjectId && x.TopicCode == code && !x.IsArchived()));

        public Task InsertAsync(Topic topic, CancellationToken ct) { Items.Add(topic); return Task.CompletedTask; }
        public Task UpdateAsync(Topic topic, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAudienceProfileRepo : IAudienceProfileRepository
    {
        public List<AudienceProfile> Items { get; } = new();

        public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AudienceProfile>)Items.Where(p => p.TenantId == t && !p.IsDeleted).ToList());

        public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p =>
                p.TenantId == t && !p.IsDeleted && p.ProfileCode == code && !p.IsArchived()));

        public Task InsertAsync(AudienceProfile profile, CancellationToken ct) { Items.Add(profile); return Task.CompletedTask; }
        public Task UpdateAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeConceptNodeRepo : IConceptNodeRepository
    {
        public List<ConceptNode> Items { get; } = new();

        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(n => n.TenantId == t && n.Id == id && !n.IsDeleted));

        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items.Where(n => n.TenantId == t && !n.IsDeleted).ToList());

        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items
                .Where(n => n.TenantId == t && !n.IsDeleted && n.SubjectId == subjectId).ToList());

        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid subjectId, Guid typeId, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(n =>
                n.TenantId == t && !n.IsDeleted && n.SubjectId == subjectId && n.ConceptTypeId == typeId
                && n.ConceptNodeCode == code && !n.IsArchived()));

        public Task InsertAsync(ConceptNode n, CancellationToken ct) { Items.Add(n); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptNode n, CancellationToken ct) => Task.CompletedTask;
    }
}
