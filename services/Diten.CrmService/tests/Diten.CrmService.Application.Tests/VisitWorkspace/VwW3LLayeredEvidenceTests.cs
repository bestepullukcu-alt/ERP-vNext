using System.Net;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.VisitEvidence;
using Diten.CrmService.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W3L · W3-BE-d — the layered visit evidence rules on the PRODUCTION resolver, readers, handlers and the Platform
/// seat client: field-level inheritance (a legal entity changes two fields, a business unit one), the order business unit
/// &gt; legal entity &gt; tenant, the exception still on top, K-L4 (one / ambiguous / none / an empty assignment falls to the
/// model), K-L5 (no seat / the Platform failing skips the layer with its note), the scope API (versioned write 409, archive
/// + history, an unpublished business unit 400, "applies to N reps"), the roster's bulk seat read (calls counted), K-L7
/// (country position codes), tenant isolation, and the old tenant document on a real mongod (no backfill).
/// </summary>
public sealed class VwW3LLayeredEvidenceTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000d3");
    private static readonly Guid OtherTenant = Guid.Parse("11110000-0000-0000-0000-0000000000d3");
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid LeUz = Guid.Parse("0e000000-0000-0000-0000-00000000000a");
    private static readonly Guid LeTr = Guid.Parse("0e000000-0000-0000-0000-00000000000b");
    private const string RepA = "aaaa0000-0000-0000-0000-00000000000a";
    private const string RepB = "aaaa0000-0000-0000-0000-00000000000b";
    private const string RepC = "aaaa0000-0000-0000-0000-00000000000c";
    private const string RepD = "aaaa0000-0000-0000-0000-00000000000d";

    private readonly FakeVisitEvidencePolicyRepository _policies = new();
    private readonly FakeLayerRepository _layers = new();
    private readonly FakeSeatReader _seats = new();
    private readonly FakeTerritoryModelRepo _models = new();
    private readonly FakeTerritoryResourceAssignmentRepo _assignments = new();
    private readonly ExceptionStore _exceptions = new();
    private readonly RecordingChanges _changes = new();
    private readonly RecordingAudit _audit = new();
    private readonly FakeTerritoryReferenceValidator _references = new();
    private readonly TerritoryModel _model;

    public VwW3LLayeredEvidenceTests()
    {
        _model = Model(Tenant);
        _references.Vocabulary[TerritoryReferenceSets.BusinessUnitValueSet] = new(["alpha", "beta"], StringComparer.OrdinalIgnoreCase);
    }

    // ═══ 1 · the layers: inheritance, order, the exception on top ═════════════════════════════════════════════

    [Fact]
    public async Task L1_a_legal_entity_changes_two_fields_a_business_unit_one_and_each_field_names_its_source()
    {
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.LegalEntity, LeUz.ToString("D"), l => { l.RadiusMeters = 500; l.PhotoMode = VisitPhotoModes.Required; });
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => l.RadiusMeters = 800);
        _seats.Seat(RepA, LeUz);
        Assign(RepA, "alpha");
        _seats.Seat(RepB, LeUz);
        Assign(RepB, "beta");                      // a business unit without a layer: the legal entity's radius stays

        var a = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal(("800", "business-unit:alpha"), Field(a, "radiusMeters"));       // BU > LE
        Assert.Equal(("required", "legal-entity:" + LeUz.ToString("D")), Field(a, "photoMode")); // the LE field the BU left empty
        Assert.Equal(("required", "tenant"), Field(a, "locationMode"));               // nobody changed it
        Assert.Equal(("150", "tenant"), Field(a, "maxAccuracyMeters"));
        Assert.Equal(800, a.Policy.RadiusMeters);
        Assert.Equal((LeUz, "alpha", "resolved"), (a.ScopeChain.LegalEntity, a.ScopeChain.BusinessUnit, a.ScopeChain.Note));
        Assert.Equal(2, a.AppliedLayers!.Count);

        var b = await Resolver().ResolveAsync(Tenant, RepB, Now, default);
        Assert.Equal(("500", "legal-entity:" + LeUz.ToString("D")), Field(b, "radiusMeters"));
        Assert.Equal("beta", b.ScopeChain.BusinessUnit);
        Assert.Null(_policies.Stored);                                                // no document is written by a read
    }

    [Fact]
    public async Task L2_the_business_unit_wins_over_the_legal_entity_on_every_field_both_set_and_the_tenant_value_is_kept_when_neither_does()
    {
        _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, ShortVisitSeconds = 90, QrMode = VisitQrModes.Optional, Version = 3 };
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.LegalEntity, LeUz.ToString("D"), l => { l.QrMode = VisitQrModes.Required; l.DoctorConfirmation = false; });
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => { l.QrMode = VisitQrModes.Off; });
        _seats.Seat(RepA, LeUz);
        Assign(RepA, "alpha");

        var e = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal(("off", "business-unit:alpha"), Field(e, "qrMode"));
        Assert.Equal(("false", "legal-entity:" + LeUz.ToString("D")), Field(e, "doctorConfirmation"));
        Assert.Equal(("90", "tenant"), Field(e, "shortVisitSeconds"));
        Assert.False(e.IsDefault);
    }

    [Fact]
    public async Task L3_the_time_boxed_exception_still_relaxes_on_top_of_the_layers()
    {
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => { l.PhotoMode = VisitPhotoModes.Required; l.QrMode = VisitQrModes.Required; });
        Assign(RepA, "alpha");
        var exception = new VisitEvidenceException
        {
            TenantId = Tenant, ResourceId = RepA, Status = VisitEvidenceExceptionStatuses.Approved, Reason = "r",
            StartDate = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1), EndDate = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1),
            Rules = [VisitEvidenceExceptionRules.PhotoOptional, VisitEvidenceExceptionRules.QrOptional]
        };
        _exceptions.Items.Add(exception);

        var e = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal("optional", e.Policy.PhotoMode);
        var photo = e.Fields.Single(f => f.Field == "photoMode");
        Assert.Equal((EvidenceFieldSources.Exception, exception.Id), (photo.Source, photo.ExceptionId));
        Assert.Equal(("optional", "exception"), Field(e, "qrMode"));                   // the BU's "required" relaxed to optional
        Assert.Equal(exception.Id, e.PrimaryException!.Id);
    }

    // ═══ 2 · K-L4 the business unit, K-L5 the missing data ═════════════════════════════════════════════════

    [Fact]
    public async Task L4_one_business_unit_is_taken_several_are_ambiguous_and_skipped_none_is_skipped_an_empty_assignment_falls_to_the_model()
    {
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => l.RadiusMeters = 800);
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.LegalEntity, LeUz.ToString("D"), l => l.RadiusMeters = 500);

        // two primary assignments in two business units → ambiguous: the BU layer is NOT applied (never the first one)
        _seats.Seat(RepA, LeUz);
        Assign(RepA, "alpha");
        Assign(RepA, "beta");
        var ambiguous = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Null(ambiguous.ScopeChain.BusinessUnit);
        Assert.Equal(EvidenceScopeNotes.BusinessUnitAmbiguous, ambiguous.ScopeChain.BusinessUnitNote);
        Assert.Equal(EvidenceScopeNotes.BusinessUnitAmbiguous, ambiguous.ScopeChain.Note);
        Assert.Equal(("500", "legal-entity:" + LeUz.ToString("D")), Field(ambiguous, "radiusMeters"));

        // the assignment carries no business unit → the model's
        var model = Model(Tenant, "alpha");
        Assign(RepB, model: model);
        var fromModel = await Resolver().ResolveAsync(Tenant, RepB, Now, default);
        Assert.Equal(("alpha", EvidenceScopeNotes.Resolved), (fromModel.ScopeChain.BusinessUnit, fromModel.ScopeChain.BusinessUnitNote));
        Assert.Equal(("800", "business-unit:alpha"), Field(fromModel, "radiusMeters"));

        // no assignment at all / neither the assignment nor the model has one / only a secondary assignment
        Assert.Equal(EvidenceScopeNotes.NoTerritory, (await Resolver().ResolveAsync(Tenant, RepC, Now, default)).ScopeChain.BusinessUnitNote);
        Assign(RepD);
        Assert.Equal(EvidenceScopeNotes.NoBusinessUnit, (await Resolver().ResolveAsync(Tenant, RepD, Now, default)).ScopeChain.BusinessUnitNote);
        Assign("rep-secondary", "alpha", primary: false);
        Assert.Equal(EvidenceScopeNotes.NoTerritory, (await Resolver().ResolveAsync(Tenant, "rep-secondary", Now, default)).ScopeChain.BusinessUnitNote);

        // the pure rule: the same answer for the same pairs; case-insensitive duplicates are ONE unit
        var a1 = Assignment("x", ["Alpha"]);
        var a2 = Assignment("x", ["alpha"]);
        Assert.Equal("Alpha", EvidenceBusinessUnitReader.Decide([(a1, _model), (a2, _model)]).Code);
    }

    [Fact]
    public async Task L5_no_seat_or_a_failing_platform_skips_the_legal_entity_with_its_note_and_the_business_unit_still_applies()
    {
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.LegalEntity, LeUz.ToString("D"), l => { l.RadiusMeters = 500; l.PhotoMode = VisitPhotoModes.Required; });
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => l.RadiusMeters = 800);
        Assign(RepA, "alpha");

        // the Platform answers: no primary seat
        var noSeat = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal((null, EvidenceScopeNotes.NoSeat), (noSeat.ScopeChain.LegalEntity, noSeat.ScopeChain.LegalEntityNote));
        Assert.Equal(EvidenceScopeNotes.NoSeat, noSeat.ScopeChain.Note);
        Assert.Equal(("optional", "tenant"), Field(noSeat, "photoMode"));
        Assert.Equal(("800", "business-unit:alpha"), Field(noSeat, "radiusMeters"));

        // the Platform fails (timeout / error / refusal): the same skip, its own note — the start is never blocked
        _seats.Seat(RepA, LeUz);
        _seats.Fail = true;
        var down = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal((null, EvidenceScopeNotes.PlatformUnavailable), (down.ScopeChain.LegalEntity, down.ScopeChain.LegalEntityNote));
        Assert.Equal(EvidenceScopeNotes.PlatformUnavailable, down.ScopeChain.Note);
        Assert.Equal(("optional", "tenant"), Field(down, "photoMode"));
        Assert.Equal(("800", "business-unit:alpha"), Field(down, "radiusMeters"));

        // both layers skipped: the reasons together, the legal entity first
        Assert.Equal("platform-unavailable,no-territory", (await Resolver().ResolveAsync(Tenant, RepC, Now, default)).ScopeChain.Note);
    }

    [Fact]
    public async Task L6_without_a_legal_entity_layer_the_platform_is_not_asked_and_without_a_chain_resolver_the_old_answer_stands()
    {
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => l.RadiusMeters = 800);
        Assign(RepA, "alpha");
        var e = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal(0, _seats.SingleCalls + _seats.BulkCalls);
        Assert.Equal((EvidenceScopeNotes.NoLayer, EvidenceScopeNotes.Resolved), (e.ScopeChain.LegalEntityNote, e.ScopeChain.Note));

        // the resolver before W3L (no layers, no chain): tenant ⊕ exception, "unresolved"
        var old = await new VisitEvidencePolicyResolver(_policies, _exceptions).ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal("unresolved", old.ScopeChain.Note);
        Assert.All(old.Fields, f => Assert.Equal("tenant", f.Source));
    }

    // ═══ 3 · the scope API ═════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task L7_a_scope_is_created_replaced_at_its_version_and_a_stale_write_is_a_409_with_its_history_and_audit()
    {
        var h = Scopes();
        var le = LeUz.ToString("D").ToUpperInvariant();                 // the route may carry any case: stored "D"
        var created = await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("legal-entity", le, new(RadiusMeters: 500, PhotoMode: "required")), default);
        Assert.Equal(200, created.StatusCode);
        Assert.Equal(LeUz.ToString("D"), created.Data!.ScopeRef);
        Assert.Equal("500", created.Data.Fields.Single(f => f.Field == "radiusMeters").Value);
        Assert.Null(created.Data.Fields.Single(f => f.Field == "qrMode").Value);  // inherited
        Assert.Equal(new[] { "created", "set:photoMode", "set:radiusMeters" },
            _changes.Rows.Select(r => r.Field is null ? r.Kind : r.Kind + ":" + r.Field).OrderBy(x => x));
        Assert.All(_changes.Rows, r => Assert.Equal(("legal-entity", LeUz.ToString("D")), (r.Scope, r.ScopeRef)));
        Assert.Single(_audit.Events, x => x.Event == VisitEvidenceAuditEvents.ScopeWritten);

        // a second create of the same key without its version loses
        Assert.Equal(409, (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("legal-entity", le, new(RadiusMeters: 600)), default)).StatusCode);

        // the replace at version 0: radius dropped (cleared — inherited again), qr set
        _changes.Rows.Clear();
        var replaced = await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("legal-entity", le, new(PhotoMode: "required", QrMode: "required", ExpectedVersion: 0)), default);
        Assert.Equal((200, 1), (replaced.StatusCode, replaced.Data!.Version));
        Assert.Equal(new[] { "cleared:radiusMeters:500→", "set:qrMode:→required" },
            _changes.Rows.Select(r => r.Kind + ":" + r.Field + ":" + r.OldValue + "→" + r.NewValue).OrderBy(x => x));

        // a stale version (0 again) is a conflict, nothing written
        var before = _changes.Rows.Count;
        var stale = await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("legal-entity", le, new(PhotoMode: "optional", ExpectedVersion: 0)), default);
        Assert.Equal(409, stale.StatusCode);
        Assert.Contains(VisitExecutionErrorCodes.ConcurrencyConflict, stale.Errors);
        Assert.Equal(before, _changes.Rows.Count);

        // the store refusing the replace (a concurrent writer) is the same 409
        _layers.StaleVersion = 1;
        Assert.Equal(409, (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("legal-entity", le, new(PhotoMode: "optional", ExpectedVersion: 1)), default)).StatusCode);
    }

    [Fact]
    public async Task L8_a_scope_write_is_refused_for_an_unpublished_business_unit_a_bad_key_an_empty_or_an_out_of_bounds_body()
    {
        var h = Scopes();
        var unknown = await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "gamma", new(RadiusMeters: 500)), default);
        Assert.Equal(400, unknown.StatusCode);
        Assert.Contains(VisitEvidenceScopeErrors.BusinessUnitUnknown, unknown.Errors);
        _references.MissingSets.Add(TerritoryReferenceSets.BusinessUnitValueSet);  // the set itself unpublished: fail-closed
        Assert.Contains(VisitEvidenceScopeErrors.BusinessUnitUnknown,
            (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "alpha", new(RadiusMeters: 500)), default)).Errors);
        _references.MissingSets.Clear();

        foreach (var (type, key) in new[] { ("legal-entity", "not-a-guid"), ("legal-entity", Guid.Empty.ToString()), ("tenant", "x"), ("brand", "alpha"), ("business-unit", " ") })
        {
            var res = await h.Handle(new UpsertVisitEvidencePolicyScopeCommand(type, key, new(RadiusMeters: 500)), default);
            Assert.True(res.StatusCode == 400 && res.Errors.Contains(VisitEvidenceScopeErrors.Invalid), type + "/" + key);
        }

        Assert.Contains(VisitEvidenceScopeErrors.Invalid, (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "alpha", new()), default)).Errors);
        Assert.Contains(VisitExecutionErrorCodes.PolicyInvalid, (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "alpha", new(RadiusMeters: 10)), default)).Errors);
        Assert.Contains(VisitExecutionErrorCodes.PolicyInvalid, (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "alpha", new(AntiFraudMode: "block")), default)).Errors);
        Assert.Empty(_layers.Items);
        Assert.Empty(_changes.Rows);
        Assert.Empty(_audit.Events);
        Assert.Equal(200, (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "Alpha", new(RadiusMeters: 500)), default)).StatusCode);
    }

    [Fact]
    public async Task L9_removing_a_scope_archives_it_writes_the_history_and_its_reps_fall_back_at_once()
    {
        var h = Scopes();
        Assign(RepA, "alpha");
        await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "alpha", new(RadiusMeters: 800)), default);
        Assert.Equal("800", Field(await Resolver().ResolveAsync(Tenant, RepA, Now, default), "radiusMeters").Value);

        Assert.Equal(404, (await h.Handle(new ArchiveVisitEvidencePolicyScopeCommand("business-unit", "beta", null), default)).StatusCode);
        Assert.Equal(409, (await h.Handle(new ArchiveVisitEvidencePolicyScopeCommand("business-unit", "alpha", 7), default)).StatusCode);
        var archived = await h.Handle(new ArchiveVisitEvidencePolicyScopeCommand("business-unit", "alpha", 0), default);
        Assert.Equal((200, "archived"), (archived.StatusCode, archived.Data!.Status));

        var layer = Assert.Single(_layers.Items);                     // archived, NOT deleted
        Assert.Equal((VisitEvidencePolicyLayerStatuses.Archived, false), (layer.Status, layer.IsDeleted));
        Assert.NotNull(layer.ArchivedAt);
        Assert.Contains(_changes.Rows, r => r.Kind == VisitEvidencePolicyChangeKinds.Archived && r.ScopeRef == "alpha" && r.Field is null);
        Assert.Contains(_audit.Events, x => x.Event == VisitEvidenceAuditEvents.ScopeArchived);
        Assert.Equal(("300", "tenant"), Field(await Resolver().ResolveAsync(Tenant, RepA, Now, default), "radiusMeters"));
        Assert.Equal(404, (await Reads().Handle(new GetVisitEvidencePolicyScopeQuery("business-unit", "alpha"), default)).StatusCode);

        // the key is free again
        Assert.Equal(200, (await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "alpha", new(RadiusMeters: 900)), default)).StatusCode);
    }

    [Fact]
    public async Task L10_the_scope_list_counts_the_reps_each_scope_applies_to_with_one_bulk_seat_read()
    {
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.LegalEntity, LeUz.ToString("D"), l => { l.RadiusMeters = 500; l.PhotoMode = "required"; });
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => l.RadiusMeters = 800);
        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "beta", l => l.QrMode = "optional");
        _seats.Seat(RepA, LeUz); Assign(RepA, "alpha");
        _seats.Seat(RepB, LeUz); Assign(RepB, "beta");
        _seats.Seat(RepC, LeTr); Assign(RepC, "alpha");
        Assign(RepD);

        var list = (await Reads(Roster(RepA, RepB, RepC, RepD)).Handle(new ListVisitEvidencePolicyScopesQuery(), default)).Data!;
        Assert.Equal(4, list.TotalReps);
        Assert.Equal(new[] { "tenant::10:4", "legal-entity:" + LeUz.ToString("D") + ":2:2", "business-unit:alpha:1:2", "business-unit:beta:1:1" },
            list.Items.Select(i => $"{i.ScopeType}:{i.ScopeRef}:{i.OverriddenFields}:{i.RepCount}"));
        Assert.True(list.Items[0].IsDefault);
        Assert.Equal((0, 1), (_seats.SingleCalls, _seats.BulkCalls));

        // the Platform failing: the legal entity counts what it can and says so
        _seats.Fail = true;
        var down = (await Reads(Roster(RepA, RepB, RepC, RepD)).Handle(new ListVisitEvidencePolicyScopesQuery(), default)).Data!;
        var leRow = down.Items.Single(i => i.ScopeType == "legal-entity");
        Assert.Equal((0, EvidenceScopeNotes.PlatformUnavailable), (leRow.RepCount, leRow.RepCountNote));
        Assert.Equal(2, down.Items.Single(i => i.ScopeRef == "alpha").RepCount);  // the business unit still counts
    }

    [Fact]
    public async Task L11_the_roster_rows_carry_the_resolved_chain_from_ONE_bulk_seat_read_per_page_and_none_without_a_legal_entity_layer()
    {
        var reps = Enumerable.Range(1, 30).Select(i => $"bbbb0000-0000-0000-0000-{i:D12}").ToArray();
        foreach (var rep in reps) { _seats.Seat(rep, LeUz); Assign(rep, "alpha"); }

        var noLayer = (await RosterReads(reps).Handle(new ListVisitEvidenceRepRosterQuery(null), default)).Data!;
        Assert.Equal(0, _seats.SingleCalls + _seats.BulkCalls);
        Assert.All(noLayer.Items, r => Assert.Equal(("alpha", EvidenceScopeNotes.NoLayer), (r.ScopeChain!.BusinessUnit, r.ScopeChain.LegalEntityNote)));

        _layers.Add(Tenant, VisitEvidencePolicyScopeTypes.LegalEntity, LeUz.ToString("D"), l => l.RadiusMeters = 500);
        var page = (await RosterReads(reps).Handle(new ListVisitEvidenceRepRosterQuery(null, 1, 25), default)).Data!;
        Assert.Equal((25, 30L), (page.Items.Count, page.Total));
        Assert.Equal((0, 1), (_seats.SingleCalls, _seats.BulkCalls));                  // never one call per rep
        Assert.Equal(25, _seats.BulkSizes.Single());
        Assert.All(page.Items, r => Assert.Equal((LeUz, "alpha", "resolved"), (r.ScopeChain!.LegalEntity, r.ScopeChain.BusinessUnit, r.ScopeChain.Note)));

        // the picker read is unchanged (no chain)
        var picker = (await RosterReads(reps).Handle(new ListVisitEvidenceRepsQuery(null), default)).Data!;
        Assert.All(picker, r => Assert.Null(r.ScopeChain));
    }

    [Fact]
    public async Task L12_the_effective_read_shows_the_sources_and_the_chain_and_the_history_filters_by_scope()
    {
        var h = Scopes();
        _seats.Seat(RepA, LeUz);
        Assign(RepA, "alpha");
        await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("legal-entity", LeUz.ToString(), new(PhotoMode: "required")), default);
        await h.Handle(new UpsertVisitEvidencePolicyScopeCommand("business-unit", "alpha", new(RadiusMeters: 800)), default);
        _changes.Rows.Add(new VisitEvidencePolicyChange { TenantId = Tenant, At = Now, Scope = "tenant", Field = "qrMode", Kind = "set" });

        var effective = (await RosterReads([RepA]).Handle(new GetVisitEvidenceEffectiveQuery(RepA), default)).Data!;
        Assert.Equal("business-unit:alpha", effective.Fields.Single(f => f.Field == "radiusMeters").Source);
        Assert.Equal("legal-entity:" + LeUz.ToString("D"), effective.Fields.Single(f => f.Field == "photoMode").Source);
        Assert.Equal((LeUz, "alpha", "resolved", "resolved", "resolved"),
            (effective.ScopeChain.LegalEntity, effective.ScopeChain.BusinessUnit, effective.ScopeChain.Note, effective.ScopeChain.LegalEntityNote, effective.ScopeChain.BusinessUnitNote));

        var reads = RosterReads([RepA]);
        var le = (await reads.Handle(new GetVisitEvidencePolicyHistoryQuery(Scope: "legal-entity", ScopeRef: LeUz.ToString().ToUpperInvariant()), default)).Data!;
        Assert.All(le.Items, i => Assert.Equal(("legal-entity", LeUz.ToString("D")), (i.Scope, i.ScopeRef)));
        Assert.Equal(2, le.Total);                                                      // created + photoMode
        Assert.Equal(1, (await reads.Handle(new GetVisitEvidencePolicyHistoryQuery(Scope: "tenant"), default)).Data!.Total);
        Assert.Equal(2, (await reads.Handle(new GetVisitEvidencePolicyHistoryQuery(Scope: "business-unit"), default)).Data!.Total);
        Assert.Equal(5, (await reads.Handle(new GetVisitEvidencePolicyHistoryQuery(), default)).Data!.Total); // the whole history, as before
        Assert.Equal(400, (await reads.Handle(new GetVisitEvidencePolicyHistoryQuery(Scope: "brand"), default)).StatusCode);
    }

    [Fact]
    public async Task L13_another_tenants_layers_never_apply_are_never_listed_read_or_archived()
    {
        _layers.Add(OtherTenant, VisitEvidencePolicyScopeTypes.LegalEntity, LeUz.ToString("D"), l => l.RadiusMeters = 999);
        _layers.Add(OtherTenant, VisitEvidencePolicyScopeTypes.BusinessUnit, "alpha", l => l.RadiusMeters = 998);
        _seats.Seat(RepA, LeUz);
        Assign(RepA, "alpha");

        var e = await Resolver().ResolveAsync(Tenant, RepA, Now, default);
        Assert.Equal(("300", "tenant"), Field(e, "radiusMeters"));
        Assert.Equal(new[] { "tenant" }, (await Reads(Roster(RepA)).Handle(new ListVisitEvidencePolicyScopesQuery(), default)).Data!.Items.Select(i => i.ScopeType));
        Assert.Equal(404, (await Reads().Handle(new GetVisitEvidencePolicyScopeQuery("business-unit", "alpha"), default)).StatusCode);
        Assert.Equal(404, (await Scopes().Handle(new ArchiveVisitEvidencePolicyScopeCommand("business-unit", "alpha", null), default)).StatusCode);
        Assert.All(_layers.Items, l => Assert.Equal(VisitEvidencePolicyLayerStatuses.Active, l.Status));
        // the other tenant's assignment is not this tenant's business unit either
        Assign(RepB, "beta", tenant: OtherTenant);
        Assert.Equal(EvidenceScopeNotes.NoTerritory, (await Resolver().ResolveAsync(Tenant, RepB, Now, default)).ScopeChain.BusinessUnitNote);
    }

    // ═══ 4 · K-L7 the position codes ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void L14_a_country_position_code_takes_the_rule_of_the_role_it_ends_with_and_anything_else_stays_unknown()
    {
        Assert.True(TerritoryPositionPolicy.TryResolve("medical-representative", out var exact));
        foreach (var code in new[] { "uz-medical-representative", "TR-Medical-Representative", " kz-medical-representative " })
        {
            Assert.True(TerritoryPositionPolicy.TryResolve(code, out var rule), code);
            Assert.Equal(exact, rule);
        }

        Assert.True(TerritoryPositionPolicy.TryResolve("tr-area-manager", out var area));
        Assert.Equal(new[] { "area" }, area.AllowedLevels);
        Assert.True(TerritoryPositionPolicy.TryResolve("uz-regional-manager", out var region));
        Assert.Equal(new[] { "region" }, region.AllowedLevels);
        foreach (var code in new[] { "medical-representative-x", "xmedical-representative", "-medical-representative", "uzmedical-representative", "medical-rep", "uz-manager" })
        {
            Assert.False(TerritoryPositionPolicy.TryResolve(code, out _), code);
        }
    }

    // ═══ 5 · the Platform seat client, the class maps, the real mongod ═══════════════════════════════════════

    [Fact]
    public async Task L15_the_seat_client_asks_once_per_100_users_answers_null_for_none_and_unavailable_for_every_failure()
    {
        var handler = new SeatHandler();
        var ids = Enumerable.Range(1, 150).Select(i => $"cccc0000-0000-0000-0000-{i:D12}").ToList();
        handler.Answer = req => req.Method == HttpMethod.Post
            ? Json("{\"data\":{\"items\":[{\"userId\":\"cccc0000-0000-0000-0000-000000000001\",\"primary\":{\"positionId\":\"" + Guid.NewGuid() + "\",\"positionCode\":\"uz-medical-representative\",\"organizationUnitId\":\"" + Guid.NewGuid() + "\",\"organizationUnitCode\":\"UZRO-FF\",\"legalEntityId\":\"" + LeUz + "\",\"effectiveFrom\":\"2026-01-01T00:00:00+00:00\"}}]},\"statusCode\":200,\"isSuccessful\":true}")
            : Json("{\"data\":{\"userId\":\"" + ids[0] + "\",\"primary\":null},\"statusCode\":200,\"isSuccessful\":true}");
        var reader = SeatClient(handler);

        var bulk = await reader.LookupAsync(ids.Append("not-a-guid").ToList(), default);
        Assert.Equal(2, handler.Requests.Count(r => r.Method == HttpMethod.Post));       // 100 + 50
        Assert.Equal(LeUz, bulk.Seats[ids[0]]!.LegalEntityId);
        Assert.Null(bulk.Seats[ids[1]]);
        Assert.Null(bulk.Seats["not-a-guid"]);
        Assert.Empty(bulk.Unavailable);
        Assert.All(handler.Requests, r => Assert.StartsWith("http://gateway.test/api/platform/organization/seats", r.RequestUri!.ToString()));
        Assert.All(handler.Requests, r => Assert.Equal("Bearer t", r.Headers.Authorization?.ToString()));

        var single = await reader.GetAsync(ids[0], default);
        Assert.True(single.Available);
        Assert.Null(single.Primary);
        Assert.Equal(EvidenceScopeNotes.NoSeat, EvidenceScopeChainResolver.LegalEntityOf(single).Note);
        Assert.True((await reader.GetAsync("rep-text-id", default)).Available);      // no Platform user: no call
        Assert.Equal(3, handler.Requests.Count);

        foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable })
        {
            handler.Answer = _ => new HttpResponseMessage(status);
            Assert.False((await reader.GetAsync(ids[0], default)).Available, status.ToString());
            Assert.Equal(2, (await reader.LookupAsync(ids.Take(2).ToList(), default)).Unavailable.Count);
        }

        handler.Answer = _ => Json("not json");
        Assert.False((await reader.GetAsync(ids[0], default)).Available);
        handler.Answer = _ => throw new HttpRequestException("down");
        Assert.False((await reader.GetAsync(ids[0], default)).Available);
    }

    [Fact]
    public void L16_the_layer_and_the_start_chain_round_trip_with_the_legal_entity_as_a_string()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(VisitEvidencePolicyLayer)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(VisitExecutionEvidenceScope)));
        var execution = new Domain.Entities.VisitExecution
        {
            TenantId = Tenant, PlannedVisitId = Guid.NewGuid(), ResourceId = RepA,
            EvidenceScope = new VisitExecutionEvidenceScope { LegalEntityId = LeUz, BusinessUnit = "alpha", Note = "resolved", LegalEntityNote = "resolved", BusinessUnitNote = "resolved" }
        };
        var doc = execution.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["EvidenceScope"]["LegalEntityId"].BsonType);
        var back = BsonSerializer.Deserialize<Domain.Entities.VisitExecution>(doc);
        Assert.Equal((LeUz, "alpha", "resolved"), (back.EvidenceScope!.LegalEntityId, back.EvidenceScope.BusinessUnit, back.EvidenceScope.Note));

        var layer = new VisitEvidencePolicyLayer { TenantId = Tenant, ScopeType = "business-unit", ScopeRef = "alpha", RadiusMeters = 800 };
        var layerDoc = layer.ToBsonDocument();
        Assert.Equal(BsonNull.Value, layerDoc["PhotoMode"]);                           // an inherited field is stored null
        Assert.Equal(800, BsonSerializer.Deserialize<VisitEvidencePolicyLayer>(layerDoc).RadiusMeters);
    }

    [Fact]
    public async Task L17_on_a_real_mongod_the_old_tenant_document_reads_as_the_tenant_layer_with_no_backfill_and_one_active_layer_per_key()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "W3L_BED_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            typeof(Diten.CrmService.Persistence.DependencyInjection)
                .GetMethod("TryEnsureIndexes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, [db]);

            // a tenant document written before W3E / W3L: the eight W3 fields only (the W3E fields removed from the stored
            // document — exactly what an older document looks like; nothing knows about scopes)
            var policyId = Guid.NewGuid();
            await db.GetCollection<VisitEvidencePolicy>(VisitEvidencePolicyRepository.CollectionName).InsertOneAsync(new VisitEvidencePolicy
            {
                Id = policyId, TenantId = Tenant, Version = 4, LocationMode = "info", RadiusMeters = 450, PhotoMode = "required", UpdatedBy = "old"
            });
            var policies = db.GetCollection<BsonDocument>(VisitEvidencePolicyRepository.CollectionName);
            await policies.UpdateOneAsync(FilterDefinition<BsonDocument>.Empty,
                Builders<BsonDocument>.Update.Unset("QrMode").Unset("AntiFraudMode"));
            Assert.False((await policies.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync()).Contains("QrMode"));
            var before = (await policies.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync()).ToJson();

            var layers = new VisitEvidencePolicyLayerRepository(db);
            var policyRepo = new VisitEvidencePolicyRepository(db);
            _seats.Seat(RepA, LeUz);
            Assign(RepA, "alpha");
            var resolver = new VisitEvidencePolicyResolver(policyRepo, null, layers, Chains());
            var e = await resolver.ResolveAsync(Tenant, RepA, Now, default);
            Assert.Equal(("450", "tenant"), Field(e, "radiusMeters"));
            Assert.Equal(("off", "tenant"), Field(e, "qrMode"));                          // the missing field reads its default
            Assert.False(e.IsDefault);

            var list = (await new VisitEvidencePolicyScopeQueryHandlers(TenantFactory.Tenant(Tenant), policyRepo, layers, Roster(RepA), Chains(), new W3LClock(Now))
                .Handle(new ListVisitEvidencePolicyScopesQuery(), default)).Data!;
            Assert.Equal(("tenant", 4, false), (list.Items.Single().ScopeType, list.Items.Single().Version, list.Items.Single().IsDefault));
            Assert.Equal(before, (await policies.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync()).ToJson()); // nothing backfilled
            Assert.Equal(0, await db.GetCollection<BsonDocument>(VisitEvidencePolicyLayerRepository.CollectionName).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

            // the layer store: one ACTIVE layer per key (the database refuses a second), a stale replace loses, an archived
            // layer frees its key, another tenant's layer is invisible
            var first = new VisitEvidencePolicyLayer { TenantId = Tenant, ScopeType = "business-unit", ScopeRef = "alpha", RadiusMeters = 800 };
            Assert.True(await layers.InsertAsync(first, default));
            Assert.False(await layers.InsertAsync(new VisitEvidencePolicyLayer { TenantId = Tenant, ScopeType = "business-unit", ScopeRef = "alpha", RadiusMeters = 1 }, default));
            Assert.True(await layers.InsertAsync(new VisitEvidencePolicyLayer { TenantId = OtherTenant, ScopeType = "business-unit", ScopeRef = "alpha", RadiusMeters = 2 }, default));
            Assert.Equal(("800", "business-unit:alpha"), Field(await resolver.ResolveAsync(Tenant, RepA, Now, default), "radiusMeters"));
            var loaded = (await layers.GetActiveAsync(Tenant, "business-unit", "ALPHA", default))!;
            loaded.RadiusMeters = 900;
            Assert.True(await layers.ReplaceAsync(loaded, 0, default));
            loaded.RadiusMeters = 950;
            Assert.False(await layers.ReplaceAsync(loaded, 0, default));                  // stale
            loaded.Status = VisitEvidencePolicyLayerStatuses.Archived;
            Assert.True(await layers.ReplaceAsync(loaded, 1, default));
            Assert.Null(await layers.GetActiveAsync(Tenant, "business-unit", "alpha", default));
            Assert.True(await layers.InsertAsync(new VisitEvidencePolicyLayer { TenantId = Tenant, ScopeType = "business-unit", ScopeRef = "alpha", RadiusMeters = 700 }, default));
            Assert.Single(await layers.ListActiveAsync(Tenant, default));

            // the history: one scope in the database
            var changes = new VisitEvidencePolicyChangeRepository(db);
            await changes.InsertManyAsync([
                new VisitEvidencePolicyChange { TenantId = Tenant, At = Now, Scope = "tenant", Field = "qrMode", Kind = "set" },
                new VisitEvidencePolicyChange { TenantId = Tenant, At = Now, Scope = "business-unit", ScopeRef = "alpha", Field = "radiusMeters", Kind = "set" },
                new VisitEvidencePolicyChange { TenantId = OtherTenant, At = Now, Scope = "business-unit", ScopeRef = "alpha", Field = "radiusMeters", Kind = "set" }], default);
            Assert.Equal(1, (await changes.ListAsync(Tenant, "business-unit", "ALPHA", 0, 10, default)).Total);
            Assert.Equal(1, (await changes.ListAsync(Tenant, "tenant", null, 0, 10, default)).Total);
            Assert.Equal(2, (await changes.ListAsync(Tenant, 0, 10, default)).Total);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════

    private EvidenceScopeChainResolver Chains() => new(new EvidenceBusinessUnitReader(_models, _assignments), _seats);

    private VisitEvidencePolicyResolver Resolver() => new(_policies, _exceptions, _layers, Chains());

    private VisitEvidenceScopeHandlers Scopes()
        => new(TenantFactory.Tenant(Tenant), new NullActorContext(), _layers, _changes, _references, _audit, new W3LClock(Now));

    private VisitEvidencePolicyScopeQueryHandlers Reads(IVisitEvidenceRepRoster? roster = null)
        => new(TenantFactory.Tenant(Tenant), _policies, _layers, roster ?? Roster(), Chains(), new W3LClock(Now));

    private VisitEvidencePolicyReadHandlers RosterReads(IReadOnlyList<string> reps)
        => new(TenantFactory.Tenant(Tenant), _changes, Resolver(), Roster(reps.ToArray()), new W3LClock(Now), null, _layers, Chains());

    private static FixedRoster Roster(params string[] reps) => new(reps.Select(r => new EvidenceRepDto(r, "Rep " + r[^2..])).ToList());

    private static (string Value, string Source) Field(EffectiveEvidencePolicy e, string field)
    {
        var f = e.Fields.Single(x => x.Field == field);
        return (f.Value, f.Source);
    }

    private TerritoryModel Model(Guid tenant, params string[] bu)
    {
        var model = new TerritoryModel
        {
            TenantId = tenant, ModelCode = "M-" + Guid.NewGuid().ToString("N")[..6], Name = "Model", Status = "active",
            EffectiveFrom = Now.AddYears(-1),
            BusinessScopes = bu.Select(c => new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = c }).ToList()
        };
        _models.Items.Add(model);
        return model;
    }

    private static TerritoryResourceAssignment Assignment(string rep, string[] bu, bool primary = true, Guid? tenant = null, Guid? model = null) => new()
    {
        TenantId = tenant ?? Tenant, ModelId = model ?? Guid.Empty, TerritoryId = Guid.NewGuid(), IsPrimary = primary, Status = "active",
        ValidFrom = Now.AddMonths(-1), Resource = new TerritoryResourceRef { ResourceId = rep, DisplayName = rep },
        BusinessScopes = bu.Select(c => new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = c }).ToList()
    };

    private void Assign(string rep, string? bu = null, bool primary = true, Guid? tenant = null, TerritoryModel? model = null)
    {
        var m = model ?? (tenant is { } t && t != Tenant ? Model(t) : _model);
        _assignments.Items.Add(Assignment(rep, bu is null ? [] : [bu], primary, tenant, m.Id));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static PlatformOrganizationSeatReader SeatClient(HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:BaseUrl"] = "http://gateway.test" }).Build();
        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer t";
        return new PlatformOrganizationSeatReader(new HttpClient(handler), config, new HttpContextAccessor { HttpContext = http }, TenantFactory.Tenant(Tenant));
    }

    private sealed class SeatHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Answer { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(Answer(request));
        }
    }

    private sealed class ExceptionStore : IVisitEvidenceExceptionRepository
    {
        public List<VisitEvidenceException> Items { get; } = new();
        public Task<VisitEvidenceException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id));
        public Task<IReadOnlyList<VisitEvidenceException>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.ToList());
        public Task<IReadOnlyList<VisitEvidenceException>> ListByResourceAsync(Guid tenantId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.Where(e => e.TenantId == tenantId && e.ResourceId == resourceId).ToList());
        public Task InsertAsync(VisitEvidenceException entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(VisitEvidenceException entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }
}
