using System.Reflection;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments;
using Diten.Platform.Application.Features.EvidenceLinking;
using Diten.Platform.Application.Features.EvidenceLinking.Events;
using Diten.Platform.Application.Features.EvidenceLinking.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.EvidenceLinking;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Platform.Application.Tests.EvidenceLinking;

/// <summary>
/// WP-CL-BE-2 — MOD-0031 evidence linking slice 1. Handler tests run over tenant-filtering in-memory fakes (the fake
/// repositories apply the SAME tenant scoping the real TenantRepository does, so isolation is exercised) and a
/// capturing outbox writer that also runs the production <see cref="EventPayloadContractValidator"/>. The access gate
/// is tested separately against the REAL <see cref="DocumentAccessEvaluator"/>.
/// </summary>
public sealed class EvidenceLinkingTests
{
    private static readonly Guid TenantA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    private const string Quote = "Reduced symptoms by 30% vs placebo (p<0.01).";

    // ============================================================ fixture

    private sealed class Store
    {
        public List<ControlledDocument> Documents { get; } = [];
        public List<ControlledDocumentVersion> Versions { get; } = [];
        public List<ExternalDocumentRegisterEntry> External { get; } = [];
        public List<EvidenceLink> Links { get; } = [];
        public Diten.Platform.Application.Tests.DocumentManagement.FakeDocumentMasterRegisterRepository Register { get; } = new();
    }

    private sealed class Fixture
    {
        public Store Store { get; }
        public TenantContext Tenant { get; } = new();
        public FakeGate Gate { get; } = new();
        public FakeReferenceData ReferenceData { get; } = new();
        public CapturingEventWriter Events { get; } = new();

        public Fixture(Guid tenant, Store? shared = null)
        {
            Store = shared ?? new Store();
            Tenant.SetTenant(tenant);
        }

        private FakeLinkRepo Links() => new(Store, Tenant);
        private FakeDocumentRepo Documents() => new(Store, Tenant);
        private FakeVersionRepo Versions() => new(Store, Tenant);
        private FakeExternalRepo External() => new(Store, Tenant);

        public CreateEvidenceLinkCommandHandler Create() => new(Tenant, new User(), Links(), Documents(), Versions(),
            External(), Gate, ReferenceData, new ImmediateExecutor(), Events);

        public RemoveEvidenceLinkCommandHandler Remove() => new(Tenant, new User(), Links(), new ImmediateExecutor(), Events);
        public GetEvidenceLinksByObjectQueryHandler ByObject() => new(Tenant, Links(), Resolver());
        public GetEvidenceLinkByIdQueryHandler ById() => new(Tenant, Links(), Resolver());
        public GetEvidenceLinksByDocumentQueryHandler ByDocument() => new(Tenant, Links(), Documents(), External(), Gate, Resolver());
        public QueryEvidenceLinksByObjectsQueryHandler Query() => new(Tenant, Links(), Resolver());

        public EvidenceDocumentStateResolver Resolver() => new(Documents(), Versions(), Store.Register, External(), Clock);
        public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

        public DocumentMasterRegisterEntry SeedRegister(ControlledDocument doc, ControlledDocumentLifecycleStatus status,
            DateTimeOffset? nextReview = null)
        {
            var row = new DocumentMasterRegisterEntry
            {
                TenantId = Tenant.TenantId, DocumentTitle = doc.Title, ControlledDocumentId = doc.Id,
                LifecycleStatus = status, NextReviewDueDate = nextReview
            };
            Store.Register.Items.Add(row);
            return row;
        }
        public GetEvidenceDocumentOptionsQueryHandler Options() => new(Tenant, Documents(), External(), Gate);

        public (ControlledDocument Doc, ControlledDocumentVersion V1, ControlledDocumentVersion V2) SeedControlled(
            string title = "SmPC Product X")
        {
            var doc = new ControlledDocument
            {
                TenantId = Tenant.TenantId, DocumentKey = "DOC-" + Guid.NewGuid().ToString("N")[..6],
                CollectionInstanceId = Guid.NewGuid(), CollectionPath = "/qms", Title = title,
                DocumentType = DocumentType.Policy, CurrentVersionNumber = 2
            };
            var v1 = Version(doc, 1);
            var v2 = Version(doc, 2);
            doc.CurrentVersionId = v2.Id;
            Store.Documents.Add(doc);
            Store.Versions.AddRange([v1, v2]);
            return (doc, v1, v2);
        }

        public ControlledDocumentVersion Version(ControlledDocument doc, int number) => new()
        {
            TenantId = Tenant.TenantId, DocumentId = doc.Id, VersionNumber = number, Checksum = "c", UploadedBy = "u",
            FileRef = new ContentRef
            {
                ContentId = Guid.NewGuid(), StorageProvider = "fs", ObjectKey = "k", FileName = "f.pdf",
                MediaType = "application/pdf", Checksum = "c"
            }
        };

        public ExternalDocumentRegisterEntry SeedExternal(string title = "EU GMP Annex 1")
        {
            var entry = new ExternalDocumentRegisterEntry
            {
                TenantId = Tenant.TenantId, ExternalDocumentTitle = title, ExternalAuthorityName = "EMA",
                SourceReference = "ref", SourceVersion = "Rev. 2022", ExternalDocumentCode = "ANNEX-1",
                CountryCode = "EU", SourceStatus = ExternalSourceStatus.Unknown
            };
            Store.External.Add(entry);
            return entry;
        }

        public Task<Response<EvidenceLinkDto>> LinkControlled(Guid documentId, Guid? versionId, string? quote = Quote,
            string type = "smpc-pil", string objectId = "claim-1", string? page = "4",
            IReadOnlyList<EvidenceSupportedSpanInput>? spans = null)
            => Create().Handle(new CreateEvidenceLinkCommand(
                new EvidenceObjectRefInput("crm", "claim", objectId, "1.0"), "controlled", documentId, versionId, type,
                new EvidenceLocatorInput(quote, "4.1", page), spans), default);
    }

    // ============================================================ controlled / external create

    [Fact]
    public async Task Controlled_link_pins_the_version_and_snapshots_title_and_label()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        var r = await fx.LinkControlled(doc.Id, v1.Id,
            spans: [new EvidenceSupportedSpanInput("tr", "semptomları azaltır", 0, 19)]);

        Assert.Equal(201, r.StatusCode);
        var dto = r.Data!;
        Assert.Equal(v1.Id, dto.DocumentVersionId);
        Assert.Equal("v1", dto.DocumentVersionLabel);
        Assert.Equal("SmPC Product X", dto.DocumentTitle);
        Assert.Equal(EvidenceLinkStatuses.Active, dto.Status);
        Assert.Equal("crm", dto.ObjectRef.Module);
        Assert.Single(dto.SupportedSpans);
        Assert.Single(fx.Store.Links);
        Assert.Equal(TenantA, fx.Store.Links[0].TenantId);
    }

    [Fact]
    public async Task Version_of_another_document_is_400()
    {
        var fx = new Fixture(TenantA);
        var (doc, _, _) = fx.SeedControlled("A");
        var (_, otherV1, _) = fx.SeedControlled("B");
        var r = await fx.LinkControlled(doc.Id, otherV1.Id);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(EvidenceLinkReasonCodes.VersionDocumentMismatch, r.ReasonCode);
        Assert.Empty(fx.Store.Links);
    }

    [Fact]
    public async Task Deleted_version_is_400_and_missing_version_id_is_400()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        v1.DeletedAt = DateTimeOffset.UtcNow;
        var deleted = await fx.LinkControlled(doc.Id, v1.Id);
        Assert.Equal(400, deleted.StatusCode);
        Assert.Equal(EvidenceLinkReasonCodes.VersionDeleted, deleted.ReasonCode);

        var noVersion = await fx.LinkControlled(doc.Id, null);
        Assert.Equal(EvidenceLinkReasonCodes.VersionRequired, noVersion.ReasonCode);
    }

    [Fact]
    public async Task Unreadable_document_is_403_and_nothing_is_written()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        fx.Gate.DenyControlled.Add(doc.Id);
        var r = await fx.LinkControlled(doc.Id, v1.Id);
        Assert.Equal(403, r.StatusCode);
        Assert.Equal(EvidenceLinkReasonCodes.DocumentNotReadable, r.ReasonCode);
        Assert.Empty(fx.Store.Links);
        Assert.Empty(fx.Events.Events);

        var ext = fx.SeedExternal();
        fx.Gate.ExternalAllowed = false;
        var er = await fx.Create().Handle(new CreateEvidenceLinkCommand(new EvidenceObjectRefInput("crm", "claim", "c"),
            "external", ext.Id, null, "literature", new EvidenceLocatorInput(Quote), null), default);
        Assert.Equal(403, er.StatusCode);
    }

    [Fact]
    public async Task External_link_has_no_version_id_and_snapshots_source_version()
    {
        var fx = new Fixture(TenantA);
        var ext = fx.SeedExternal();
        var r = await fx.Create().Handle(new CreateEvidenceLinkCommand(
            new EvidenceObjectRefInput("CRM", "Claim-Country-Version", "cv-9"), "external", ext.Id, null, "Literature",
            new EvidenceLocatorInput(Quote, Page: "12"), null), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Null(r.Data!.DocumentVersionId);
        Assert.Equal("Rev. 2022", r.Data.DocumentVersionLabel);
        Assert.Equal("EU GMP Annex 1", r.Data.DocumentTitle);
        Assert.Equal("claim-country-version", r.Data.ObjectRef.ObjectType); // normalised
        Assert.Equal("literature", r.Data.EvidenceTypeCode);

        var withVersion = await fx.Create().Handle(new CreateEvidenceLinkCommand(
            new EvidenceObjectRefInput("crm", "claim", "cv-9"), "external", ext.Id, Guid.NewGuid(), "literature",
            new EvidenceLocatorInput(Quote), null), default);
        Assert.Equal(EvidenceLinkReasonCodes.VersionNotAllowed, withVersion.ReasonCode);
    }

    [Fact]
    public async Task Unknown_or_foreign_document_is_404()
    {
        var fx = new Fixture(TenantA);
        Assert.Equal(404, (await fx.LinkControlled(Guid.NewGuid(), Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Evidence_type_must_be_active_in_brd_and_a_missing_set_is_reference_set_missing()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();

        var invalid = await fx.LinkControlled(doc.Id, v1.Id, type: "hearsay");
        Assert.Equal(400, invalid.StatusCode);
        Assert.Equal(EvidenceLinkReasonCodes.InvalidEvidenceType, invalid.ReasonCode);

        fx.ReferenceData.SetMissing = true;
        var missing = await fx.LinkControlled(doc.Id, v1.Id);
        Assert.Equal(400, missing.StatusCode);
        Assert.Equal(EvidenceLinkReasonCodes.ReferenceSetMissing, missing.ReasonCode);
        Assert.Empty(fx.Store.Links);
        Assert.Equal(EvidenceReferenceSets.EvidenceType, fx.ReferenceData.LastSet);
    }

    [Fact]
    public async Task Quote_is_required_and_field_limits_apply()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        var empty = await fx.LinkControlled(doc.Id, v1.Id, quote: "   ");
        Assert.Equal(400, empty.StatusCode);
        Assert.Equal(EvidenceLinkReasonCodes.QuoteRequired, empty.ReasonCode);

        Assert.Equal(400, (await fx.LinkControlled(doc.Id, v1.Id, quote: new string('q', 1001))).StatusCode);
        var tooManySpans = Enumerable.Range(0, 11).Select(i => new EvidenceSupportedSpanInput("en", "s" + i)).ToList();
        Assert.Equal(400, (await fx.LinkControlled(doc.Id, v1.Id, spans: tooManySpans)).StatusCode);
        Assert.Equal(400, (await fx.LinkControlled(doc.Id, v1.Id,
            spans: [new EvidenceSupportedSpanInput("en", "x", 5, 2)])).StatusCode);
    }

    [Fact]
    public async Task Same_object_document_version_page_quote_active_is_409_but_other_page_is_fine()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, v2) = fx.SeedControlled();
        Assert.Equal(201, (await fx.LinkControlled(doc.Id, v1.Id)).StatusCode);

        var dup = await fx.LinkControlled(doc.Id, v1.Id, quote: "  " + Quote + " ");
        Assert.Equal(409, dup.StatusCode);
        Assert.Equal(EvidenceLinkReasonCodes.DuplicateLink, dup.ReasonCode);

        Assert.Equal(201, (await fx.LinkControlled(doc.Id, v1.Id, page: "5")).StatusCode);
        Assert.Equal(201, (await fx.LinkControlled(doc.Id, v2.Id)).StatusCode);
    }

    [Fact]
    public async Task Storage_level_duplicate_is_translated_to_409()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        FakeLinkRepo.SimulateRaceOnce = true;
        try
        {
            var r = await fx.LinkControlled(doc.Id, v1.Id);
            Assert.Equal(409, r.StatusCode);
            Assert.Empty(fx.Events.Events);
        }
        finally
        {
            FakeLinkRepo.SimulateRaceOnce = false;
        }
    }

    // ============================================================ remove + queries

    [Fact]
    public async Task Remove_needs_a_reason_keeps_the_record_and_shows_it_with_includeRemoved()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        var link = (await fx.LinkControlled(doc.Id, v1.Id)).Data!;

        var noReason = await fx.Remove().Handle(new RemoveEvidenceLinkCommand(link.LinkId, " "), default);
        Assert.Equal(EvidenceLinkReasonCodes.RemovalReasonRequired, noReason.ReasonCode);

        var removed = await fx.Remove().Handle(new RemoveEvidenceLinkCommand(link.LinkId, "wrong page"), default);
        Assert.Equal(200, removed.StatusCode);
        Assert.Equal(EvidenceLinkStatuses.Removed, removed.Data!.Status);
        Assert.Equal("wrong page", removed.Data.RemovalReason);
        Assert.Single(fx.Store.Links); // record stays

        var again = await fx.Remove().Handle(new RemoveEvidenceLinkCommand(link.LinkId, "x"), default);
        Assert.Equal(409, again.StatusCode);

        var active = await fx.ByObject().Handle(new GetEvidenceLinksByObjectQuery("crm", "claim", "claim-1", null, false), default);
        Assert.Empty(active.Data!);
        var all = await fx.ByObject().Handle(new GetEvidenceLinksByObjectQuery("CRM", "claim", "claim-1", null, true), default);
        Assert.Single(all.Data!);
        Assert.Equal(EvidenceLinkStatuses.Removed, all.Data![0].Status);

        // Removal frees the unique space: the same evidence can be linked again.
        Assert.Equal(201, (await fx.LinkControlled(doc.Id, v1.Id)).StatusCode);
    }

    [Fact]
    public async Task Object_query_filters_by_object_version_and_get_by_id_works()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        var link = (await fx.LinkControlled(doc.Id, v1.Id)).Data!;
        Assert.Single((await fx.ByObject().Handle(new GetEvidenceLinksByObjectQuery("crm", "claim", "claim-1", "1.0", false), default)).Data!);
        Assert.Empty((await fx.ByObject().Handle(new GetEvidenceLinksByObjectQuery("crm", "claim", "claim-1", "2.0", false), default)).Data!);
        Assert.Equal(400, (await fx.ByObject().Handle(new GetEvidenceLinksByObjectQuery("crm", null, "claim-1", null, false), default)).StatusCode);
        Assert.Equal(link.LinkId, (await fx.ById().Handle(new GetEvidenceLinkByIdQuery(link.LinkId), default)).Data!.LinkId);
        Assert.Equal(404, (await fx.ById().Handle(new GetEvidenceLinkByIdQuery(Guid.NewGuid()), default)).StatusCode);
    }

    [Fact]
    public async Task Reverse_lookup_returns_only_active_links_and_honours_version_and_access()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, v2) = fx.SeedControlled();
        var a = (await fx.LinkControlled(doc.Id, v1.Id, objectId: "claim-1")).Data!;
        await fx.LinkControlled(doc.Id, v1.Id, objectId: "claim-2");
        await fx.LinkControlled(doc.Id, v2.Id, objectId: "claim-3");
        await fx.Remove().Handle(new RemoveEvidenceLinkCommand(a.LinkId, "superseded"), default);

        var byDoc = (await fx.ByDocument().Handle(new GetEvidenceLinksByDocumentQuery(doc.Id, null), default)).Data!;
        Assert.Equal(new[] { "claim-2", "claim-3" }, byDoc.Select(x => x.ObjectRef.ObjectId).OrderBy(x => x).ToArray());

        var byVersion = (await fx.ByDocument().Handle(new GetEvidenceLinksByDocumentQuery(doc.Id, v1.Id), default)).Data!;
        Assert.Equal("claim-2", byVersion.Single().ObjectRef.ObjectId);

        fx.Gate.DenyControlled.Add(doc.Id);
        Assert.Equal(403, (await fx.ByDocument().Handle(new GetEvidenceLinksByDocumentQuery(doc.Id, null), default)).StatusCode);
    }

    [Fact]
    public async Task Tenant_isolation_links_and_documents_of_another_tenant_are_invisible()
    {
        var a = new Fixture(TenantA);
        var (doc, v1, _) = a.SeedControlled();
        var link = (await a.LinkControlled(doc.Id, v1.Id)).Data!;

        var b = new Fixture(TenantB, a.Store);
        Assert.Equal(404, (await b.ById().Handle(new GetEvidenceLinkByIdQuery(link.LinkId), default)).StatusCode);
        Assert.Empty((await b.ByObject().Handle(new GetEvidenceLinksByObjectQuery("crm", "claim", "claim-1", null, true), default)).Data!);
        Assert.Empty((await b.ByDocument().Handle(new GetEvidenceLinksByDocumentQuery(doc.Id, null), default)).Data!);
        Assert.Equal(404, (await b.Remove().Handle(new RemoveEvidenceLinkCommand(link.LinkId, "x"), default)).StatusCode);
        Assert.Equal(404, (await b.LinkControlled(doc.Id, v1.Id)).StatusCode); // A's document does not resolve in B
        Assert.Empty((await b.Options().Handle(new GetEvidenceDocumentOptionsQuery(null, null, null), default)).Data!);
        Assert.Equal(EvidenceLinkStatuses.Active, a.Store.Links.Single().Status);
    }

    // ============================================================ events

    [Fact]
    public async Task Create_and_remove_each_enqueue_exactly_one_event_without_quote_or_span_text()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        var link = (await fx.LinkControlled(doc.Id, v1.Id, quote: "QUOTE-SECRET-MARKER",
            spans: [new EvidenceSupportedSpanInput("en", "SPAN-SECRET-TEXT")])).Data!;

        var created = Assert.Single(fx.Events.Events);
        var createdEvent = Assert.IsType<EvidenceLinkCreatedV1>(created);
        Assert.Equal("platform.evidence.link.created.v1", createdEvent.EventName);
        Assert.Equal(link.LinkId, createdEvent.LinkId);
        Assert.Equal(TenantA, createdEvent.TenantId);
        Assert.Equal(v1.Id, createdEvent.DocumentVersionId);
        Assert.Equal("smpc-pil", createdEvent.EvidenceTypeCode);

        await fx.Remove().Handle(new RemoveEvidenceLinkCommand(link.LinkId, "REASON-SECRET"), default);
        Assert.Equal(2, fx.Events.Events.Count);
        var removedEvent = Assert.IsType<EvidenceLinkRemovedV1>(fx.Events.Events[1]);
        Assert.Equal("platform.evidence.link.removed.v1", removedEvent.EventName);

        foreach (var e in fx.Events.Events)
        {
            var json = JsonSerializer.Serialize(e, e.GetType());
            Assert.DoesNotContain("QUOTE-SECRET-MARKER", json);
            Assert.DoesNotContain("SPAN-SECRET-TEXT", json);
            Assert.DoesNotContain("REASON-SECRET", json);
        }
    }

    [Fact]
    public void Event_names_and_payloads_pass_the_production_contract_validator()
    {
        var validator = new EventPayloadContractValidator();
        validator.Validate(new EvidenceLinkCreatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, TenantA, Guid.NewGuid(), null,
            Guid.NewGuid(), "crm", "claim", "1", null, "controlled", Guid.NewGuid(), Guid.NewGuid(), "literature"));
        validator.Validate(new EvidenceLinkRemovedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, TenantA, Guid.NewGuid(), null,
            Guid.NewGuid(), "crm", "claim", "1", null, "external", Guid.NewGuid(), null, "literature"));
    }

    // ============================================================ document options

    [Fact]
    public async Task Document_options_return_only_readable_documents()
    {
        var fx = new Fixture(TenantA);
        var (readable, _, v2) = fx.SeedControlled("Readable SmPC");
        var (hidden, _, _) = fx.SeedControlled("Hidden SmPC");
        var (deleted, _, _) = fx.SeedControlled("Deleted SmPC");
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        fx.Gate.DenyControlled.Add(hidden.Id);
        fx.SeedExternal("Annex SmPC guideline");

        var rows = (await fx.Options().Handle(new GetEvidenceDocumentOptionsQuery("smpc", null, null), default)).Data!;
        var controlled = Assert.Single(rows, r => r.Kind == EvidenceDocumentKinds.Controlled);
        Assert.Equal(readable.Id, controlled.DocumentId);
        Assert.Equal(v2.Id, controlled.CurrentVersionId);
        Assert.Equal("v2", controlled.CurrentVersionLabel);
        var external = Assert.Single(rows, r => r.Kind == EvidenceDocumentKinds.External);
        Assert.Equal("Rev. 2022", external.SourceVersion);
        Assert.Equal("EU", external.CountryCode);

        fx.Gate.ExternalAllowed = false;
        var noExternal = (await fx.Options().Handle(new GetEvidenceDocumentOptionsQuery(null, "external", null), default)).Data!;
        Assert.Empty(noExternal);
        Assert.Single((await fx.Options().Handle(new GetEvidenceDocumentOptionsQuery(null, "controlled", 1), default)).Data!);
        Assert.Equal(400, (await fx.Options().Handle(new GetEvidenceDocumentOptionsQuery(null, "internal", null), default)).StatusCode);
    }

    // ============================================================ access gate over the REAL MOD-0029 evaluator

    [Fact]
    public async Task Gate_uses_the_mod0029_evaluator_fail_closed()
    {
        var doc = new ControlledDocument
        {
            TenantId = TenantA, DocumentKey = "K", CollectionInstanceId = Guid.NewGuid(), CollectionPath = "/", Title = "T",
            OwnerCompanyId = Guid.NewGuid()
        };
        var entry = new ExternalDocumentRegisterEntry
        {
            TenantId = TenantA, ExternalDocumentTitle = "E", ExternalAuthorityName = "A", SourceReference = "R"
        };

        var nobody = Gate(new DocumentPrincipal(Guid.NewGuid(), [], [Guid.NewGuid()]));
        Assert.False(await nobody.CanReadControlledAsync(doc, default)); // no register link, no grant ⇒ denied
        Assert.False(await nobody.CanReadExternalAsync(entry, default));

        var admin = Gate(new DocumentPrincipal(Guid.NewGuid(), [], [], IsTenantAdmin: true));
        Assert.True(await admin.CanReadControlledAsync(doc, default));
        Assert.True(await admin.CanReadExternalAsync(entry, default));

        var registerReader = Gate(new DocumentPrincipal(Guid.NewGuid(), [], [Guid.NewGuid()],
            Permissions: [ExternalDocumentPermissions.View]));
        Assert.True(await registerReader.CanReadExternalAsync(entry, default));
    }

    private static EvidenceDocumentAccessGate Gate(DocumentPrincipal principal)
        => new(new DocumentAccessEvaluator(
            new Diten.Platform.Application.Tests.DocumentManagement.FakeFolderDocumentAccessPolicyRepository(),
            new Diten.Platform.Application.Tests.DocumentManagement.FakeDocumentShareRecordRepository(),
            new Diten.Platform.Application.Tests.DocumentManagement.FakePrincipalAccessor(principal)));

    // ============================================================ WP-CL-BE-5 — computed document state + bulk query

    [Fact]
    public async Task Controlled_link_pinned_to_an_older_version_of_an_effective_document_reads_superseded()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, v2) = fx.SeedControlled();
        var due = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        fx.SeedRegister(doc, ControlledDocumentLifecycleStatus.Effective, due);
        await fx.LinkControlled(doc.Id, v1.Id, objectId: "old");
        await fx.LinkControlled(doc.Id, v2.Id, objectId: "current");

        var old = Assert.Single((await fx.ByObject().Handle(
            new GetEvidenceLinksByObjectQuery("crm", "claim", "old", null, false), default)).Data!);
        Assert.True(old.IsSuperseded);
        Assert.Equal(EvidenceDocumentStates.Effective, old.DocumentState);
        Assert.Equal(v2.Id, old.CurrentVersionId);
        Assert.Equal("v2", old.CurrentVersionLabel);
        Assert.Equal(due, old.ReviewDueAt);

        var current = Assert.Single((await fx.ByObject().Handle(
            new GetEvidenceLinksByObjectQuery("crm", "claim", "current", null, false), default)).Data!);
        Assert.False(current.IsSuperseded);
    }

    [Fact]
    public async Task Version_stamped_superseded_by_docmgmt_reads_superseded_even_when_ids_match()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        doc.CurrentVersionId = v1.Id; // current pointer not moved, but DocMgmt stamped v1 superseded
        v1.VersionStatus = DocumentVersionStatus.Superseded;
        fx.SeedRegister(doc, ControlledDocumentLifecycleStatus.Effective);
        var link = (await fx.LinkControlled(doc.Id, v1.Id)).Data!;

        var dto = (await fx.ById().Handle(new GetEvidenceLinkByIdQuery(link.LinkId), default)).Data!;
        Assert.True(dto.IsSuperseded);
    }

    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Suspended, EvidenceDocumentStates.Suspended, false)]
    [InlineData(ControlledDocumentLifecycleStatus.Retired, EvidenceDocumentStates.Retired, false)]
    [InlineData(ControlledDocumentLifecycleStatus.ObsoleteCopy, EvidenceDocumentStates.Retired, false)]
    [InlineData(ControlledDocumentLifecycleStatus.Superseded, EvidenceDocumentStates.Retired, true)]
    [InlineData(ControlledDocumentLifecycleStatus.UnderRevision, EvidenceDocumentStates.Effective, false)]
    [InlineData(ControlledDocumentLifecycleStatus.Draft, EvidenceDocumentStates.Unknown, false)]
    public async Task Register_lifecycle_maps_to_document_state(
        ControlledDocumentLifecycleStatus lifecycle, string expectedState, bool expectedSuperseded)
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled(); // v1 pinned, v2 current
        fx.SeedRegister(doc, lifecycle);
        await fx.LinkControlled(doc.Id, v1.Id);

        var dto = Assert.Single((await fx.ByObject().Handle(
            new GetEvidenceLinksByObjectQuery("crm", "claim", "claim-1", null, false), default)).Data!);
        Assert.Equal(expectedState, dto.DocumentState);
        Assert.Equal(expectedSuperseded, dto.IsSuperseded);
    }

    [Fact]
    public async Task No_register_row_is_unknown_and_never_guessed_superseded()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, v2) = fx.SeedControlled();
        await fx.LinkControlled(doc.Id, v1.Id);

        var dto = Assert.Single((await fx.ByObject().Handle(
            new GetEvidenceLinksByObjectQuery("crm", "claim", "claim-1", null, false), default)).Data!);
        Assert.Equal(EvidenceDocumentStates.Unknown, dto.DocumentState);
        Assert.False(dto.IsSuperseded);
        Assert.Equal(v2.Id, dto.CurrentVersionId);
    }

    [Fact]
    public async Task External_source_superseded_date_withdrawal_and_monitoring_due_are_computed()
    {
        var fx = new Fixture(TenantA);
        var superseded = fx.SeedExternal("Old guidance");
        superseded.SourceStatus = ExternalSourceStatus.CurrentEffective;
        superseded.SourceSupersededDate = fx.Clock.GetUtcNow().AddDays(-1);
        var withdrawn = fx.SeedExternal("Withdrawn guidance");
        withdrawn.SourceStatus = ExternalSourceStatus.Withdrawn;
        var effective = fx.SeedExternal("Current guidance");
        effective.SourceStatus = ExternalSourceStatus.CurrentEffective;
        effective.SourceSupersededDate = fx.Clock.GetUtcNow().AddDays(10); // announced, not yet in force
        effective.NextCheckDueDate = fx.Clock.GetUtcNow().AddDays(5);

        foreach (var (e, id) in new[] { (superseded, "e1"), (withdrawn, "e2"), (effective, "e3") })
        {
            Assert.Equal(201, (await fx.Create().Handle(new CreateEvidenceLinkCommand(
                new EvidenceObjectRefInput("crm", "claim", id, "1"), "external", e.Id, null, "regulatory-letter",
                new EvidenceLocatorInput(Quote), null), default)).StatusCode);
        }

        var rows = (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(
            [new("crm", "claim", "e1"), new("crm", "claim", "e2"), new("crm", "claim", "e3")], false), default)).Data!;
        var e1 = Assert.Single(rows[0].Links);
        Assert.True(e1.IsSuperseded);
        var e2 = Assert.Single(rows[1].Links);
        Assert.Equal(EvidenceDocumentStates.Withdrawn, e2.DocumentState);
        var e3 = Assert.Single(rows[2].Links);
        Assert.False(e3.IsSuperseded);
        Assert.Equal(EvidenceDocumentStates.Effective, e3.DocumentState);
        Assert.Equal("Rev. 2022", e3.CurrentVersionLabel);
        Assert.Equal(effective.NextCheckDueDate, e3.ReviewDueAt);
    }

    [Fact]
    public async Task Bulk_query_groups_per_object_keeps_empty_rows_and_honours_include_removed()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        await fx.LinkControlled(doc.Id, v1.Id, objectId: "a");
        var removed = (await fx.LinkControlled(doc.Id, v1.Id, objectId: "b")).Data!;
        await fx.Remove().Handle(new RemoveEvidenceLinkCommand(removed.LinkId, "wrong page"), default);
        fx.Store.Links.Single(x => x.Id == removed.LinkId).Status = EvidenceLinkStatuses.Removed;

        var r = await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(
            [new("CRM", "claim", "a"), new("crm", "claim", "b"), new("crm", "claim", "none")], false), default);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal(["a", "b", "none"], r.Data!.Select(x => x.ObjectRef.ObjectId));
        Assert.Single(r.Data![0].Links);
        Assert.Empty(r.Data![1].Links);
        Assert.Empty(r.Data![2].Links);

        var withRemoved = (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(
            [new("crm", "claim", "b")], true), default)).Data!;
        Assert.Single(withRemoved[0].Links);
    }

    [Fact]
    public async Task Bulk_query_is_tenant_isolated()
    {
        var shared = new Store();
        var a = new Fixture(TenantA, shared);
        var (doc, v1, _) = a.SeedControlled();
        await a.LinkControlled(doc.Id, v1.Id, objectId: "x");

        var b = new Fixture(TenantB, shared);
        var rows = (await b.Query().Handle(new QueryEvidenceLinksByObjectsQuery([new("crm", "claim", "x")], true), default)).Data!;
        Assert.Empty(rows[0].Links);
    }

    [Fact]
    public async Task Bulk_query_rejects_empty_over_limit_and_incomplete_objects()
    {
        var fx = new Fixture(TenantA);
        Assert.Equal(400, (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery([], false), default)).StatusCode);
        Assert.Equal(400, (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(null, false), default)).StatusCode);
        var tooMany = Enumerable.Range(0, EvidenceLinkLimits.MaxQueryObjects + 1)
            .Select(i => (EvidenceObjectRefInput?)new EvidenceObjectRefInput("crm", "claim", $"c{i}")).ToList();
        Assert.Equal(400, (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(tooMany, false), default)).StatusCode);
        var exactly = tooMany.Take(EvidenceLinkLimits.MaxQueryObjects).ToList();
        Assert.Equal(200, (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(exactly, false), default)).StatusCode);
        Assert.Equal(400, (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(
            [new("crm", "claim", " ")], false), default)).StatusCode);
    }

    [Fact]
    public async Task Same_document_version_and_quote_under_a_new_object_ref_is_not_a_duplicate()
    {
        // WP-CL-BE-5 stop-rule check: CRM copies a link onto a new claim version (new ObjectId + ObjectVersion). The
        // MOD-0031 duplicate rule keys on the object too, so the copy is a new link, never a 409.
        var fx = new Fixture(TenantA);
        var (doc, v1, _) = fx.SeedControlled();
        Assert.Equal(201, (await fx.LinkControlled(doc.Id, v1.Id, objectId: "claim-v1")).StatusCode);
        Assert.Equal(201, (await fx.LinkControlled(doc.Id, v1.Id, objectId: "claim-v2")).StatusCode);
        Assert.Equal(409, (await fx.LinkControlled(doc.Id, v1.Id, objectId: "claim-v2")).StatusCode);
    }

    [Fact]
    public async Task Resolver_reads_each_document_once_and_never_writes_docmgmt()
    {
        var fx = new Fixture(TenantA);
        var (doc, v1, v2) = fx.SeedControlled();
        fx.SeedRegister(doc, ControlledDocumentLifecycleStatus.Effective);
        await fx.LinkControlled(doc.Id, v1.Id, objectId: "p");
        await fx.LinkControlled(doc.Id, v2.Id, objectId: "q");

        // Fake repositories throw on any DocMgmt write (UpdateAsync/Create…); a successful read proves read-only.
        var rows = (await fx.Query().Handle(new QueryEvidenceLinksByObjectsQuery(
            [new("crm", "claim", "p"), new("crm", "claim", "q")], false), default)).Data!;
        Assert.True(rows[0].Links[0].IsSuperseded);
        Assert.False(rows[1].Links[0].IsSuperseded);
    }

    // ============================================================ surface + schema

    [Theory]
    [InlineData(nameof(EvidenceLinksController.Create), "POST", "links", EvidenceLinkPermissions.Manage)]
    [InlineData(nameof(EvidenceLinksController.Remove), "POST", "links/{id:guid}/remove", EvidenceLinkPermissions.Manage)]
    [InlineData(nameof(EvidenceLinksController.ListByObject), "GET", "links", EvidenceLinkPermissions.Read)]
    [InlineData(nameof(EvidenceLinksController.Get), "GET", "links/{id:guid}", EvidenceLinkPermissions.Read)]
    [InlineData(nameof(EvidenceLinksController.Query), "POST", "links/query", EvidenceLinkPermissions.Read)]
    [InlineData(nameof(EvidenceLinksController.ByDocument), "GET", "links/by-document/{documentId:guid}", EvidenceLinkPermissions.Read)]
    [InlineData(nameof(EvidenceLinksController.DocumentOptions), "GET", "document-options", EvidenceLinkPermissions.Read)]
    public void Endpoints_have_verb_route_and_permission(string action, string verb, string route, string permission)
    {
        Assert.Equal("api/v1/evidence", typeof(EvidenceLinksController).GetCustomAttribute<RouteAttribute>()!.Template);
        var method = typeof(EvidenceLinksController).GetMethod(action)!;
        var http = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Contains(verb, http.HttpMethods);
        Assert.Equal(route, http.Template);
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
    }

    [Fact]
    public void No_update_or_delete_endpoint_exists()
    {
        foreach (var method in typeof(EvidenceLinksController).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.Empty(method.GetCustomAttributes<HttpPutAttribute>());
            Assert.Empty(method.GetCustomAttributes<HttpPatchAttribute>());
            Assert.Empty(method.GetCustomAttributes<HttpDeleteAttribute>());
        }
    }

    [Fact]
    public void Schema_manifest_carries_the_evidence_links_indexes_in_the_production_union()
    {
        var collection = Assert.Single(PlatformSchemaManifest.All, c => c.Name == PlatformCollections.EvidenceLinks);
        Assert.Equal(SchemaProfile.EvidenceLinking, collection.Profile);
        Assert.Contains(collection, PlatformSchemaManifest.For(SchemaProfile.EvidenceLinking));
    }

    // ============================================================ fakes

    private sealed class User : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Email => "evidence@example.test";
        public string? DisplayName => "Evidence Tester";
        public string ActorName => "evidence@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeGate : IEvidenceDocumentAccessGate
    {
        public HashSet<Guid> DenyControlled { get; } = [];
        public bool ExternalAllowed { get; set; } = true;

        public Task<bool> CanReadControlledAsync(ControlledDocument document, CancellationToken ct)
            => Task.FromResult(!DenyControlled.Contains(document.Id));

        public Task<bool> CanReadExternalAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct)
            => Task.FromResult(ExternalAllowed);
    }

    private sealed class FakeReferenceData : IBusinessReferenceDataActiveMembershipService
    {
        private static readonly string[] Active = ["smpc-pil", "clinical-study", "literature", "internal-data", "regulatory-letter"];
        public bool SetMissing { get; set; }
        public string? LastSet { get; private set; }

        public Task<BusinessReferenceDataActiveMembershipResult> ValidateActiveValueAsync(string setCode, string valueCode, CancellationToken ct = default)
        {
            LastSet = setCode;
            if (SetMissing)
            {
                return Task.FromResult(new BusinessReferenceDataActiveMembershipResult(false, setCode, valueCode, "reference_data_set_not_found", "blocked"));
            }

            return Task.FromResult(Active.Contains(valueCode)
                ? new BusinessReferenceDataActiveMembershipResult(true, setCode, valueCode, null, "ok")
                : new BusinessReferenceDataActiveMembershipResult(false, setCode, valueCode, "reference_value_not_active", "blocked"));
        }

        public Task<BusinessReferenceDataActiveMembershipResult> ValidateActiveValuesAsync(string setCode, IEnumerable<string> valueCodes, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<BusinessReferenceDataActiveMembershipResult> EnsureSetHasActiveValuesAsync(string setCode, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Session : IPlatformTransactionSession
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
    }

    private sealed class ImmediateExecutor : IPlatformTransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
            CancellationToken cancellationToken = default) => body(new Session(), cancellationToken);
    }

    private sealed class CapturingEventWriter : ITransactionalIntegrationEventWriter
    {
        private readonly EventPayloadContractValidator _validator = new();
        public List<IIntegrationEvent> Events { get; } = [];

        public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
            EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            _validator.Validate(@event); // the production contract check runs on every captured event
            Events.Add(@event);
            return Task.FromResult(new EventEnvelope<TEvent>(new EventMetadata(options.EventId ?? Guid.NewGuid(),
                @event.EventName, @event.EventVersion, options.CorrelationId ?? Guid.NewGuid(), null, options.TenantId,
                options.Producer ?? "test", DateTimeOffset.UtcNow), @event));
        }
    }

    /// <summary>Applies the same tenant + soft-delete scoping as TenantRepository, and the partial-unique semantics of
    /// ux_evidence_links_tenant_active_dedup.</summary>
    private sealed class FakeLinkRepo(Store store, ITenantContext tenant) : IEvidenceLinkRepository
    {
        public static bool SimulateRaceOnce { get; set; }

        private IEnumerable<EvidenceLink> Scoped => store.Links.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<EvidenceLink?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<EvidenceLink>> ListByObjectAsync(string module, string objectType, string objectId,
            string? objectVersion, bool includeRemoved, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EvidenceLink>>(Scoped.Where(x => x.ObjectRef.Module == module
                && x.ObjectRef.ObjectType == objectType && x.ObjectRef.ObjectId == objectId
                && (objectVersion == null || x.ObjectRef.ObjectVersion == objectVersion)
                && (includeRemoved || x.IsActive)).ToList());

        public Task<IReadOnlyList<EvidenceLink>> ListActiveByDocumentAsync(Guid documentId, Guid? documentVersionId,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EvidenceLink>>(Scoped.Where(x => x.DocumentId == documentId && x.IsActive
                && (documentVersionId == null || x.DocumentVersionId == documentVersionId)).ToList());

        public Task<EvidenceLink?> FindActiveByDedupKeyAsync(string key, CancellationToken ct = default)
            => Task.FromResult(SimulateRaceOnce ? null : Scoped.FirstOrDefault(x => x.IsActive && x.ActiveDedupKey == key));

        public Task InsertAsync(IPlatformTransactionSession session, EvidenceLink link, CancellationToken ct = default)
        {
            Assert.NotNull(session);
            if (SimulateRaceOnce)
            {
                SimulateRaceOnce = false;
                throw new EvidenceLinkDuplicateException(new InvalidOperationException("E11000"));
            }

            if (Scoped.Any(x => x.IsActive && x.ActiveDedupKey == link.ActiveDedupKey))
            {
                throw new EvidenceLinkDuplicateException(new InvalidOperationException("E11000"));
            }

            store.Links.Add(link);
            return Task.CompletedTask;
        }

        public Task<bool> MarkRemovedAsync(IPlatformTransactionSession session, EvidenceLink link, CancellationToken ct = default)
            => Task.FromResult(Scoped.Any(x => x.Id == link.Id));
    }

    private sealed class FakeDocumentRepo(Store store, ITenantContext tenant) : IControlledDocumentRepository
    {
        private IEnumerable<ControlledDocument> Scoped => store.Documents.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ControlledDocument> CreateAsync(ControlledDocument d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ControlledDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<ControlledDocument?> GetByDocumentKeyAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ControlledDocument>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>(Scoped.ToList());
        public Task<IReadOnlyList<ControlledDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ControlledDocument>> GetByCollectionInstanceAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(ControlledDocument d, CancellationToken ct = default) => throw new NotSupportedException("DocMgmt is read-only here");
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("DocMgmt is read-only here");
    }

    private sealed class FakeVersionRepo(Store store, ITenantContext tenant) : IControlledDocumentVersionRepository
    {
        private IEnumerable<ControlledDocumentVersion> Scoped => store.Versions.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ControlledDocumentVersion> CreateAsync(ControlledDocumentVersion v, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ControlledDocumentVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ControlledDocumentVersion>> GetByDocumentAsync(Guid docId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ControlledDocumentVersion?> GetByDocumentAndNumberAsync(Guid docId, int n, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> GetMaxVersionNumberAsync(Guid docId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SupersedeActiveVersionsAsync(Guid docId, Guid except, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeExternalRepo(Store store, ITenantContext tenant) : IExternalDocumentRegisterRepository
    {
        private IEnumerable<ExternalDocumentRegisterEntry> Scoped => store.External.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ExternalDocumentRegisterEntry> CreateAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ExternalDocumentRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ExternalDocumentRegisterEntry>> ListAsync(ExternalDocumentListFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ExternalDocumentRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ExternalDocumentRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException("DocMgmt is read-only here");
    }
}
