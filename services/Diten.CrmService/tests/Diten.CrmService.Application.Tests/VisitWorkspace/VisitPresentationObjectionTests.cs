using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Handlers;
using Diten.CrmService.Application.Features.VisitPresentation;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W3 (W3-BE-b) — on the PRODUCTION handlers: the path step's presentation notes (limits, absent keeps, the
/// new-version clone copies, a published version never changes), the objection library (SoD, an approved answer never
/// changes, approved claims only) and the visit presentation read (slide choice, approved claims / answers only, a
/// foreign plan is 404, the target-time fallback, labels in the request language).
/// </summary>
public sealed class VisitPresentationObjectionTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-000000000001");
    private static readonly Guid ProductA = Guid.NewGuid(), ProductB = Guid.NewGuid();

    // ── 1 · presentation notes on a path step ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Presentation_notes_are_bounded_absent_keeps_them_a_clone_copies_and_a_published_version_never_changes()
    {
        var w = new World();
        var (path, step) = w.Path(ProductA, steps: 1, published: false);
        var objection = w.Objection(ProductA, "price", ObjectionResponseStatuses.Draft);
        var update = UpdateHandler(w);

        // out of bounds → 400, nothing written
        foreach (var bad in new[]
                 {
                     new KnowledgePathStepPresentationInput(KeyMessage: new string('k', 501)),
                     new KnowledgePathStepPresentationInput(TalkTrack: new string('t', 4001)),
                     new KnowledgePathStepPresentationInput(TargetSeconds: 9),
                     new KnowledgePathStepPresentationInput(TargetSeconds: 3601),
                     new KnowledgePathStepPresentationInput(Transition: new string('x', 501)),
                     new KnowledgePathStepPresentationInput(ObjectionResponseIds: Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList())
                 })
        {
            var refused = await update.Handle(Update(path, step, bad), default);
            Assert.Equal((400, KnowledgePathPresentationLimits.Invalid), (refused.StatusCode, refused.Errors![0]));
        }

        var foreign = await update.Handle(Update(path, step, new KnowledgePathStepPresentationInput(ObjectionResponseIds: new[] { Guid.NewGuid() })), default);
        Assert.Equal((400, KnowledgePathPresentationLimits.ObjectionNotFound), (foreign.StatusCode, foreign.Errors![0]));
        Assert.Null(step.Presentation);

        // written
        var notes = new KnowledgePathStepPresentationInput("Ana mesaj", "Konuşma metni", 75, "Geçiş", new[] { objection.Id });
        Assert.True((await update.Handle(Update(path, step, notes), default)).IsSuccessful);
        Assert.Equal(("Ana mesaj", 75, objection.Id), (step.Presentation!.KeyMessage, step.Presentation.TargetSeconds, step.Presentation.ObjectionResponseIds.Single()));

        // an older client sends no presentation: the notes stay
        Assert.True((await update.Handle(Update(path, step, null), default)).IsSuccessful);
        Assert.Equal("Ana mesaj", step.Presentation!.KeyMessage);

        // an empty object clears them
        Assert.True((await update.Handle(Update(path, step, new KnowledgePathStepPresentationInput()), default)).IsSuccessful);
        Assert.Null(step.Presentation);

        // publish freezes: an update is refused and the notes do not move
        step.Presentation = KnowledgePathPresentation.Map(notes);
        path.PathStatus = KnowledgePathStatuses.Published;
        path.StepSetFrozenAt = DateTimeOffset.UtcNow;
        var frozen = await update.Handle(Update(path, step, new KnowledgePathStepPresentationInput("değişti")), default);
        Assert.Equal(409, frozen.StatusCode);
        Assert.Equal("Ana mesaj", step.Presentation!.KeyMessage);

        // the new-version clone copies the notes (a separate list, the source untouched)
        var clone = await new CreateKnowledgePathVersionHandler(w, new Actor("author"), w.Paths)
            .Handle(new CreateKnowledgePathVersionCommand(path.Id), default);
        Assert.True(clone.IsSuccessful, string.Join(" / ", clone.Errors ?? Array.Empty<string>()));
        var copy = Assert.Single(w.Paths.Items.Single(p => p.Id == clone.Data).Steps);
        Assert.NotEqual(step.StepId, copy.StepId);
        Assert.Equal(("Ana mesaj", "Konuşma metni", 75, "Geçiş"), (copy.Presentation!.KeyMessage, copy.Presentation.TalkTrack, copy.Presentation.TargetSeconds, copy.Presentation.Transition));
        Assert.Equal(new[] { objection.Id }, copy.Presentation.ObjectionResponseIds);
        Assert.NotSame(step.Presentation.ObjectionResponseIds, copy.Presentation.ObjectionResponseIds);
    }

    // ── 2 · the objection library ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_answer_rests_on_approved_claims_its_author_cannot_approve_it_and_an_approved_answer_never_changes()
    {
        var w = new World();
        var approvedClaim = w.Claim("Etkinlik %40 daha yüksek", approved: true);
        var draftClaim = w.Claim("Taslak iddia", approved: false);
        var rules = new ObjectionResponseRules(w, w.Claims);

        var notApproved = await new CreateObjectionResponseHandler(w, new Actor("author"), w.Objections, rules)
            .Handle(new CreateObjectionResponseCommand(ProductA, "price", "Pahalı değil mi?", "Uzun vadede maliyet …", new[] { draftClaim.Id }), default);
        Assert.Equal((400, ObjectionResponseErrors.ClaimNotApproved), (notApproved.StatusCode, notApproved.Errors![0]));
        var badCategory = await new CreateObjectionResponseHandler(w, new Actor("author"), w.Objections, rules)
            .Handle(new CreateObjectionResponseCommand(ProductA, "competitor_card", "Q", "A", null), default);
        Assert.Equal((400, ObjectionResponseErrors.CategoryInvalid), (badCategory.StatusCode, badCategory.Errors![0]));

        var created = await new CreateObjectionResponseHandler(w, new Actor("author"), w.Objections, rules)
            .Handle(new CreateObjectionResponseCommand(ProductA, "price", "Pahalı değil mi?", "Uzun vadede maliyet …", new[] { approvedClaim.Id }), default);
        Assert.True(created.IsSuccessful, string.Join(" / ", created.Errors ?? Array.Empty<string>()));
        var id = created.Data;

        // SoD: the author (any case) cannot approve
        var self = await new ApproveObjectionResponseHandler(w, new Actor("AUTHOR"), w.Objections, rules)
            .Handle(new ApproveObjectionResponseCommand(id), default);
        Assert.Equal(403, self.StatusCode); // the author is refused
        Assert.Equal((403, ObjectionResponseErrors.SeparationOfDuties), (self.StatusCode, self.Errors![0]));
        Assert.Equal(ObjectionResponseStatuses.Draft, w.Objections.Items.Single().Status);

        var ok = await new ApproveObjectionResponseHandler(w, new Actor("approver"), w.Objections, rules)
            .Handle(new ApproveObjectionResponseCommand(id), default);
        Assert.True(ok.IsSuccessful);
        Assert.Equal((ObjectionResponseStatuses.Approved, "approver"), (w.Objections.Items.Single().Status, w.Objections.Items.Single().ApprovedBy));

        // an approved answer never changes: update is 409, it can only be retired
        var change = await new UpdateObjectionResponseHandler(w, new Actor("author"), w.Objections, rules)
            .Handle(new UpdateObjectionResponseCommand(id, ProductA, "price", "Yeni soru", "Yeni yanıt", null), default);
        Assert.Equal((409, ObjectionResponseErrors.NotEditable), (change.StatusCode, change.Errors![0]));
        Assert.Equal("Pahalı değil mi?", w.Objections.Items.Single().Question);
        Assert.True((await new RetireObjectionResponseHandler(w, new Actor("author"), w.Objections).Handle(new RetireObjectionResponseCommand(id), default)).IsSuccessful);
        Assert.Equal(ObjectionResponseStatuses.Retired, w.Objections.Items.Single().Status);
    }

    // ── 3 · the visit presentation read ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_promo_line_shows_every_step_a_reminder_the_first_and_only_approved_claims_and_answers_reach_the_rep()
    {
        var w = new World();
        var approved = w.Claim("Onaylı iddia", approved: true, evidence: "Smith 2024");
        var draft = w.Claim("Onaysız iddia", approved: false, evidence: "Gizli 2025");
        var (promoPath, _) = w.Path(ProductA, steps: 3, published: true, claims: new[] { approved, draft });
        var (reminderPath, _) = w.Path(ProductB, steps: 2, published: true);
        var answer = w.Objection(ProductA, "side_effect", ObjectionResponseStatuses.Approved, approved);
        var draftAnswer = w.Objection(ProductA, "price", ObjectionResponseStatuses.Draft);
        var retired = w.Objection(ProductA, "dosage", ObjectionResponseStatuses.Retired);
        promoPath.Steps[0].Presentation = new KnowledgePathStepPresentation
        {
            KeyMessage = "Ana mesaj", TargetSeconds = 90, ObjectionResponseIds = { draftAnswer.Id, answer.Id, retired.Id }
        };
        promoPath.Steps[1].EstimatedDurationMinutes = 2; // no target seconds ⇒ 2 × 60
        promoPath.Steps[2].EstimatedDurationMinutes = null;
        var plan = w.Plan("rep-1", PlannedVisitTargetType.Contact, (reminderPath, StrategyProductLineRoles.NonPromo, 2), (promoPath, StrategyProductLineRoles.Promo, 1));

        var dto = (await Read(w, "rep-1").Handle(new GetVisitPresentationQuery(plan.Id, "tr-TR"), default)).Data!;

        Assert.Equal(4, dto.Slides.Count); // promo: 3 steps (Order 1), reminder: its first step only (Order 2)
        Assert.Equal(new[] { 0, 1, 2, 3 }, dto.Slides.Select(s => s.Index));
        Assert.Equal(new[] { ProductA, ProductA, ProductA, ProductB }, dto.Slides.Select(s => s.ProductId));
        Assert.Equal(reminderPath.Steps[0].StepId, dto.Slides[3].StepId);
        Assert.Equal(StrategyProductLineRoles.NonPromo, dto.Slides[3].Role);
        // approved claims only, with their references
        Assert.Equal(new[] { "Onaylı iddia" }, dto.Slides[0].Bullets);
        Assert.Equal(new[] { "Smith 2024" }, dto.Slides[0].References);
        Assert.DoesNotContain(dto.Slides.SelectMany(s => s.Bullets), b => b.Contains("Onaysız"));
        // approved answers only, labelled in the request language
        var objection = Assert.Single(dto.Slides[0].Objections);
        Assert.Equal((answer.Id, "side_effect", "Yan etki"), (objection.Id, objection.CategoryCode, objection.CategoryLabel));
        Assert.Equal(new[] { "Smith 2024" }, objection.References);
        Assert.Contains(dto.ObjectionCategories, c => c.Code == "price" && c.Label == "Fiyat");
        // target time: the note, else the estimated minutes × 60, else null
        Assert.Equal((90, 120, (int?)null), (dto.Slides[0].Presentation.TargetSeconds, dto.Slides[1].Presentation.TargetSeconds, dto.Slides[2].Presentation.TargetSeconds));
        Assert.Equal("Ana mesaj", dto.Slides[0].Presentation.KeyMessage);
        // decision 8: no image for a rep
        Assert.All(dto.Slides, s => Assert.Equal(("none", (string?)null), (s.Asset.Kind, s.Asset.Url)));
        Assert.Contains(VisitPresentationWarnings.AssetNotAvailable, dto.Warnings);

        // English: the display name
        var en = (await Read(w, "rep-1").Handle(new GetVisitPresentationQuery(plan.Id, "en"), default)).Data!;
        Assert.Equal("Side effects", Assert.Single(en.Slides[0].Objections).CategoryLabel);
    }

    [Fact]
    public async Task A_pharmacy_visit_shows_the_first_step_of_every_product_a_foreign_plan_is_404_and_gaps_are_warned()
    {
        var w = new World();
        var (pathA, _) = w.Path(ProductA, steps: 3, published: true);
        var (pathB, _) = w.Path(ProductB, steps: 2, published: true);
        var pharmacy = w.Plan("rep-1", PlannedVisitTargetType.Pharmacy, (pathA, StrategyProductLineRoles.Promo, 1), (pathB, StrategyProductLineRoles.Promo, 2));

        var dto = (await Read(w, "rep-1").Handle(new GetVisitPresentationQuery(pharmacy.Id, "tr"), default)).Data!;
        Assert.Equal(new[] { pathA.Steps[0].StepId, pathB.Steps[0].StepId }, dto.Slides.Select(s => s.StepId));

        // another rep's visit is as absent as a missing one
        var foreign = await Read(w, "rep-2", readAll: false).Handle(new GetVisitPresentationQuery(pharmacy.Id, "tr"), default);
        Assert.Equal((404, VisitPresentationErrors.NotFound), (foreign.StatusCode, foreign.Errors![0]));
        Assert.Equal(404, (await Read(w, "rep-1").Handle(new GetVisitPresentationQuery(Guid.NewGuid(), "tr"), default)).StatusCode);

        // the frozen path version is gone → the frozen title, no notes, a warning; no content → no_content
        pharmacy.ContentItems[0].PathVersion = "9.9";
        var gap = (await Read(w, "rep-1").Handle(new GetVisitPresentationQuery(pharmacy.Id, "tr"), default)).Data!;
        Assert.Contains(VisitPresentationWarnings.PathVersionMissing, gap.Warnings);
        Assert.Equal("Frozen 0", gap.Slides[0].Title);
        var empty = w.Plan("rep-1", PlannedVisitTargetType.Contact);
        Assert.Equal(new[] { VisitPresentationWarnings.NoContent },
            (await Read(w, "rep-1").Handle(new GetVisitPresentationQuery(empty.Id, "tr"), default)).Data!.Warnings);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private static UpdateKnowledgePathStepHandler UpdateHandler(World w)
        => new(w, new Actor("author"), w.Paths, w.Contents, null!, objections: w.Objections);

    private static UpdateKnowledgePathStepCommand Update(KnowledgePath path, KnowledgePathStep step, KnowledgePathStepPresentationInput? notes)
        => new(path.Id, step.StepId, step.StepOrder, step.StepCode, step.StepTitle, step.StepType, step.ContentId, step.IsRequired,
            step.VersionPinPolicy, step.CompletionRule, step.PrerequisiteStepId, step.ConceptNodeId, step.EstimatedDurationMinutes,
            step.Notes, null, null, null, notes);

    private static GetVisitPresentationHandler Read(World w, string caller, bool readAll = false)
        => new(w, readAll ? TestCallerScope.Unrestricted(caller) : new TestCallerScope(caller),
            w.Plans, w.Paths, w.Contents, w.Claims, w.Objections, w);

    private sealed class Actor(string? name) : IActorContext
    {
        public string? ActorName { get; } = name;
    }

    /// <summary>One tenant's world: paths, contents, claims, answers, plans + the objection category set.</summary>
    private sealed class World : ITenantContext, IReferenceDataValidator, IReferenceDataCatalogReader
    {
        public Guid? TenantId => Tenant;
        public bool HasTenant => true;
        public PathRepo Paths { get; } = new();
        public ContentRepo Contents { get; } = new();
        public ClaimRepo Claims { get; } = new();
        public ObjectionRepo Objections { get; } = new();
        public FakePlannedVisitRepository Plans { get; } = new();

        private static readonly (string Code, string En, string Tr)[] Categories =
        [
            ("price", "Price", "Fiyat"), ("side_effect", "Side effects", "Yan etki"), ("dosage", "Dosage / administration", "Doz / kullanım")
        ];

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken cancellationToken)
            => Task.FromResult(new ReferenceValidationResult(
                Categories.Any(c => c.Code == value) ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue, setCode, value));

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
            => Task.FromResult(new ReferenceSetSnapshot(setCode, true, Categories
                .Select(c => new ReferenceValueSnapshot(c.Code, c.En, null, true, false, new Dictionary<string, string> { ["label_tr"] = c.Tr }))
                .ToList()));

        public Claim Claim(string text, bool approved, string? evidence = null)
        {
            var claim = new Claim
            {
                TenantId = Tenant, ClaimCode = "C-" + Guid.NewGuid().ToString("N")[..6], ClaimText = text,
                Status = approved ? ClaimStatuses.Approved : ClaimStatuses.Draft,
                EvidenceRefs = evidence is null ? new List<string>() : new List<string> { evidence }
            };
            Claims.Items.Add(claim);
            return claim;
        }

        public ObjectionResponse Objection(Guid product, string category, string status, params Claim[] claims)
        {
            var o = new ObjectionResponse
            {
                TenantId = Tenant, ProductId = product, CategoryCode = category, Question = "Soru " + category, Answer = "Yanıt " + category,
                Status = status, ClaimIds = claims.Select(c => c.Id).ToList(), CreatedBy = "author", Version = 1
            };
            Objections.Items.Add(o);
            return o;
        }

        public (KnowledgePath Path, KnowledgePathStep First) Path(Guid product, int steps, bool published, Claim[]? claims = null)
        {
            var path = new KnowledgePath
            {
                TenantId = Tenant, PathCode = "P-" + Guid.NewGuid().ToString("N")[..6], PathName = "Yol", PathVersion = "1.0",
                PathStatus = published ? KnowledgePathStatuses.Published : KnowledgePathStatuses.Draft,
                StepSetFrozenAt = published ? DateTimeOffset.UtcNow : null, Version = 1
            };
            for (var i = 0; i < steps; i++)
            {
                var content = new KnowledgeContent
                {
                    TenantId = Tenant, ContentCode = "K-" + i, ContentTitle = "İçerik " + i, Summary = "Özet " + i, ProductId = product,
                    ClaimRefs = (claims ?? Array.Empty<Claim>()).Select(c => new KnowledgeContentClaimRef { ClaimId = c.Id, ClaimCode = c.ClaimCode }).ToList()
                };
                Contents.Items.Add(content);
                path.Steps.Add(new KnowledgePathStep
                {
                    StepOrder = (i + 1) * 10, StepCode = "S" + i, StepTitle = "Adım " + i, StepType = KnowledgePathStepTypes.CoreMessage,
                    ContentId = content.Id, ContentCode = content.ContentCode, VersionPinPolicy = KnowledgePathVersionPin.Pinned,
                    CompletionRule = KnowledgePathCompletionRules.None
                });
            }

            Paths.Items.Add(path);
            return (path, path.Steps[0]);
        }

        public PlannedVisitEntity Plan(string resource, string targetType, params (KnowledgePath Path, string Role, int Order)[] lines)
        {
            var plan = new PlannedVisitEntity
            {
                TenantId = Tenant, TargetType = targetType, TargetId = Guid.NewGuid(), PlannedDate = new DateOnly(2026, 10, 14),
                Resource = new PlannedVisitResourceRef { ResourceId = resource, ResourceType = "user" },
                ContentItems = lines.Select(l => new PlannedVisitContentItem
                {
                    ProductId = l.Path.Steps[0].ContentId == Guid.Empty ? Guid.Empty : Contents.Items.First(c => c.Id == l.Path.Steps[0].ContentId).ProductId!.Value,
                    ProductCode = "GP-1", ProductName = "Ürün", Role = l.Role, Order = l.Order, PathId = l.Path.Id, PathVersion = l.Path.PathVersion,
                    Steps = l.Path.Steps.Select((s, i) => new PlannedVisitContentStep
                    {
                        StepId = s.StepId, ContentId = s.ContentId, Title = "Frozen " + i, Minutes = s.EstimatedDurationMinutes
                    }).ToList()
                }).ToList()
            };
            Plans.Items.Add(plan);
            return plan;
        }
    }

    private sealed class PathRepo : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();
        public Task<KnowledgePath?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));
        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgePath>>(Items.Where(p => p.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid tenantId, string pathCode, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgePath>>(Items.Where(p => p.TenantId == tenantId && p.PathCode == pathCode).ToList());
        public Task InsertAsync(KnowledgePath entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath entity, int expectedVersion, CancellationToken ct) { entity.Version = expectedVersion + 1; return Task.FromResult(true); }
    }

    private sealed class ContentRepo : IKnowledgeContentRepository
    {
        public List<KnowledgeContent> Items { get; } = new();
        public Task<KnowledgeContent?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));
        public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgeContent>>(Items.Where(c => c.TenantId == tenantId).ToList());
        public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid tenantId, string contentCode, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.ContentCode == contentCode));
        public Task InsertAsync(KnowledgeContent content, CancellationToken ct) { Items.Add(content); return Task.CompletedTask; }
        public Task UpdateAsync(KnowledgeContent content, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ClaimRepo : IClaimRepository
    {
        public List<Claim> Items { get; } = new();
        public Task<Claim?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));
        public Task<IReadOnlyList<Claim>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Claim>>(Items.ToList());
        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid tenantId, string claimCode, CancellationToken ct) => Task.FromResult<IReadOnlyList<Claim>>(Items.Where(c => c.ClaimCode == claimCode).ToList());
        public Task<Claim?> GetActiveByCodeAsync(Guid tenantId, string claimCode, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.ClaimCode == claimCode));
        public Task InsertAsync(Claim entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task UpdateAsync(Claim entity, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ObjectionRepo : IObjectionResponseRepository
    {
        public List<ObjectionResponse> Items { get; } = new();
        public Task<ObjectionResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(o => o.TenantId == tenantId && o.Id == id));
        public Task<IReadOnlyList<ObjectionResponse>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ObjectionResponse>>(Items.Where(o => o.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<ObjectionResponse>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ObjectionResponse>>(Items.Where(o => o.TenantId == tenantId && ids.Contains(o.Id)).ToList());
        public Task InsertAsync(ObjectionResponse entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(ObjectionResponse entity, int expectedVersion, CancellationToken ct) { entity.Version = expectedVersion + 1; return Task.FromResult(true); }
    }
}
