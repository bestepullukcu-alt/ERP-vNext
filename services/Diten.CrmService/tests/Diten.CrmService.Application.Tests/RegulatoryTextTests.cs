using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Application.Features.Knowledge.Regulatory;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Eventing;
using Diten.CrmService.Infrastructure.Workflow;
using Diten.CrmService.Persistence.Repositories;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-KP-5a — safety text (product × country × language) + country legal profile (country × language): Regulatory-only
/// approval through ONE MOD-0023 round (KP-REG-{CC}). Pins: the lifecycle (draft → submit → approve → active, the
/// previous active superseded; reject → draft with the decision kept; withdraw), one active + one open version per key,
/// a submitted text is frozen, rejection comment required, no self decision, template missing → 409, ObjectType routing
/// on the shared outcome consumer (claims untouched), resolve (active / missing / other key), tenant isolation, MDM
/// fail-closed, the KP-1 country / language validator, the class maps and the unique active-key indexes.
/// </summary>
public sealed class RegulatoryTextTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");
    private static readonly Guid ProductX = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private const string Author = "author-1";
    private const string Regulator = "regulator-1";
    private const string Body = "Ciddi yan etkiler:\n\n- Anafilaksi\n- Hepatotoksisite";

    // ================================================================ lifecycle

    [Fact]
    public async Task A_draft_is_submitted_to_the_country_regulatory_template_approved_and_resolved()
    {
        var fx = new Fixture();
        var draft = await fx.CreateSafetyAsync();
        Assert.Equal(("draft", 1, "SAF-TR-0001"), (draft.Status, draft.Version, draft.SafetyTextCode));

        var submitted = Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default));
        Assert.Equal(RegulatoryTextStatuses.InReview, submitted.Status);
        var start = Assert.Single(fx.Workflow.Starts);
        Assert.Equal(("KP-REG-TR", "crm.safety-text", draft.Id.ToString("D")), (start.TemplateCode, start.ObjectType, start.ObjectId));
        Assert.Equal($"/CRM/SafetyTexts/{draft.Id:D}", start.DisplayContext.DeepLinkUrl);
        Assert.Equal(fx.Workflow.LastInstanceId, submitted.WorkflowInstanceId);

        // The Regulatory reviewer decides on the SAME task (K1); the outcome event activates the text.
        var decided = await fx.DecideAsync(draft.Id, "approve", "Uygun.");
        Assert.Equal(RegulatoryTextStatuses.InReview, decided.Status);              // the state moves with the outcome
        Assert.True(Assert.Single(fx.Decisions.Calls).Approve);
        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, draft.Id, ClaimReviewOutcomes.Approved);

        var active = Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default));
        Assert.Equal((RegulatoryTextStatuses.Active, true), (active.Status, active.IsActive));
        var decision = Assert.Single(active.Decisions);
        Assert.Equal((Regulator, "approve", "Uygun."), (decision.By, decision.Outcome, decision.Comment));

        var resolved = Ok(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "tr", "TR"), default));
        Assert.Equal((draft.Id, Body), (resolved.Id, resolved.Body));             // paragraphs kept as written
    }

    [Fact]
    public async Task Approving_a_new_version_supersedes_the_previous_active_so_the_key_has_one_active()
    {
        var fx = new Fixture();
        var v1 = await fx.ActiveSafetyAsync();

        var v2 = Ok(await fx.Safety().Handle(new NewSafetyTextVersionCommand(v1.Id), default));
        Assert.Equal((2, RegulatoryTextStatuses.Draft, v1.SafetyTextCode, Body), (v2.Version, v2.Status, v2.SafetyTextCode, v2.Body));
        Ok(await fx.Safety().Handle(new UpdateSafetyTextCommand(v2.Id, "Güncel güvenlilik bilgisi."), default));
        Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(v2.Id), default));
        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, v2.Id, ClaimReviewOutcomes.Approved);

        Assert.Single(fx.SafetyRepo.Items, t => t.IsActive());
        Assert.Equal(RegulatoryTextStatuses.Superseded, fx.SafetyRepo.Items.Single(t => t.Id == v1.Id).Status);
        var resolved = Ok(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "TR", "tr"), default));
        Assert.Equal((v2.Id, 2, "Güncel güvenlilik bilgisi."), (resolved.Id, resolved.Version, resolved.Body));

        var detail = Ok(await fx.Safety().Handle(new GetSafetyTextQuery(v2.Id), default));
        Assert.Equal(new[] { 2, 1 }, detail.History!.Select(h => h.Version));
        // A superseded version can seed the next one as well.
        Assert.Equal(201, (await fx.Safety().Handle(new NewSafetyTextVersionCommand(v1.Id), default)).StatusCode);
    }

    [Fact]
    public async Task A_rejection_returns_the_text_to_draft_keeps_the_decision_and_it_can_be_resubmitted()
    {
        var fx = new Fixture();
        var draft = await fx.SubmittedSafetyAsync();

        await fx.DecideAsync(draft.Id, "reject", "Kaynak KÜB sürümü eksik.");
        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, draft.Id, ClaimReviewOutcomes.Rejected);

        fx.Actor.Name = Author;
        var back = Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default));
        Assert.Equal((RegulatoryTextStatuses.Draft, true), (back.Status, back.CanEdit));
        Assert.Equal(("reject", "Kaynak KÜB sürümü eksik."), (back.Decisions.Single().Outcome, back.Decisions.Single().Comment));
        Ok(await fx.Safety().Handle(new UpdateSafetyTextCommand(draft.Id, Body, SourceDocumentRef: "KÜB 2026-03"), default));
        Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default));
        Assert.Equal(2, fx.Workflow.Starts.Count);
        Assert.Equal("crm.safety-text:" + draft.Id.ToString("D") + ":r2", fx.Workflow.Starts[1].IdempotencyKey);
    }

    [Fact]
    public async Task Only_the_submitter_withdraws_and_the_text_returns_to_draft()
    {
        var fx = new Fixture();
        var draft = await fx.SubmittedSafetyAsync();

        fx.Actor.Name = "someone-else";
        Assert.Equal(403, (await fx.Safety().Handle(new WithdrawSafetyTextCommand(draft.Id), default)).StatusCode);

        fx.Actor.Name = Author;
        var withdrawn = Ok(await fx.Safety().Handle(new WithdrawSafetyTextCommand(draft.Id), default));
        Assert.Equal(RegulatoryTextStatuses.Draft, withdrawn.Status);
        Assert.Single(fx.Workflow.Cancels);
        Assert.Empty(withdrawn.Decisions); // a cancelled round leaves no decision
    }

    // ================================================================ key rules / frozen

    [Fact]
    public async Task One_open_version_per_key()
    {
        var fx = new Fixture();
        var draft = await fx.CreateSafetyAsync();

        Code(await fx.Safety().Handle(fx.CreateSafety(), default), 409, RegulatoryTextErrors.SafetyTextOpenDraftExists);

        Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default));
        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, draft.Id, ClaimReviewOutcomes.Approved);
        Ok(await fx.Safety().Handle(new NewSafetyTextVersionCommand(draft.Id), default));
        Code(await fx.Safety().Handle(new NewSafetyTextVersionCommand(draft.Id), default), 409,
            RegulatoryTextErrors.SafetyTextOpenDraftExists);

        // Another language of the same product is another key.
        Assert.Equal(201, (await fx.Safety().Handle(fx.CreateSafety(country: "UZ", language: "ru"), default)).StatusCode);
    }

    [Fact]
    public async Task A_submitted_or_active_text_is_frozen()
    {
        var fx = new Fixture();
        var draft = await fx.SubmittedSafetyAsync();
        fx.Actor.Name = Author;

        Code(await fx.Safety().Handle(new UpdateSafetyTextCommand(draft.Id, "x"), default), 409, RegulatoryTextErrors.NotEditable);
        Code(await fx.Safety().Handle(new ArchiveSafetyTextCommand(draft.Id), default), 409, RegulatoryTextErrors.NotEditable);
        Code(await fx.Safety().Handle(new NewSafetyTextVersionCommand(draft.Id), default), 409,
            RegulatoryTextErrors.NewVersionSourceInvalid);

        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, draft.Id, ClaimReviewOutcomes.Approved);
        Code(await fx.Safety().Handle(new UpdateSafetyTextCommand(draft.Id, "x"), default), 409, RegulatoryTextErrors.NotEditable);
        Code(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default), 409, RegulatoryTextErrors.NotEditable);

        // Archiving the active one takes it out of resolve.
        Ok(await fx.Safety().Handle(new ArchiveSafetyTextCommand(draft.Id), default));
        Code(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "TR", "tr"), default), 404,
            RegulatoryTextErrors.SafetyTextMissing);
    }

    // ================================================================ decision rules

    [Fact]
    public async Task A_rejection_needs_a_comment()
    {
        var fx = new Fixture();
        var draft = await fx.SubmittedSafetyAsync();
        fx.Actor.Name = Regulator;

        Code(await fx.Safety().Handle(new DecideSafetyTextCommand(draft.Id, "reject", "  "), default), 400,
            RegulatoryTextErrors.RejectionCommentRequired);
        Assert.Empty(fx.Decisions.Calls);
        Code(await fx.Safety().Handle(new DecideSafetyTextCommand(draft.Id, "maybe", null), default), 400,
            RegulatoryTextErrors.DecisionInvalid);
    }

    [Fact]
    public async Task The_author_or_submitter_cannot_decide_and_cannot_see_a_decision_button()
    {
        var fx = new Fixture();
        var draft = await fx.SubmittedSafetyAsync();
        fx.Actor.Name = Author;
        fx.Decisions.Mine.Add(fx.Workflow.Tasks.Single());

        Code(await fx.Safety().Handle(new DecideSafetyTextCommand(draft.Id, "approve", null), default), 403,
            RegulatoryTextErrors.SelfDecisionForbidden);
        Assert.False(Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default)).CanDecide);

        fx.Actor.Name = Regulator;
        Assert.True(Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default)).CanDecide);
    }

    [Fact]
    public async Task A_missing_regulatory_template_is_409_and_nothing_changes()
    {
        var fx = new Fixture();
        var draft = await fx.CreateSafetyAsync();
        fx.Workflow.StartOutcome = ClaimWorkflowCallOutcome.TemplateMissing;

        Code(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default), 409, RegulatoryTextErrors.ReviewTemplateMissing);
        Assert.Equal(RegulatoryTextStatuses.Draft, fx.SafetyRepo.Items.Single().Status);
        Assert.Empty(fx.SafetyRepo.Items.Single().ReviewRounds);
    }

    [Fact]
    public async Task The_template_code_format_comes_from_configuration_and_needs_the_country_slot()
    {
        var configured = new ConfigurationRegulatoryTextReviewSettings(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Crm:RegulatoryTexts:Workflow:TemplateCodeFormat"] = "REG-{0}-V2" })
            .Build());
        var broken = new ConfigurationRegulatoryTextReviewSettings(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Crm:RegulatoryTexts:Workflow:TemplateCodeFormat"] = "REG-ALL" })
            .Build());
        Assert.Equal(("REG-{0}-V2", "KP-REG-{0}"), (configured.TemplateCodeFormat, broken.TemplateCodeFormat));

        var fx = new Fixture { Settings = configured };
        var draft = await fx.CreateSafetyAsync();
        Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default));
        Assert.Equal("REG-TR-V2", fx.Workflow.Starts.Single().TemplateCode);
    }

    // ================================================================ outcome routing / reconcile

    [Fact]
    public async Task The_shared_outcome_consumer_routes_regulatory_object_types_and_claims_are_untouched()
    {
        var fx = new Fixture();
        var safety = await fx.SubmittedSafetyAsync();
        fx.Actor.Name = Author;
        var profile = Ok(await fx.Legal().Handle(fx.CreateLegal(), default));
        Ok(await fx.Legal().Handle(new SubmitCountryLegalProfileCommand(profile.Id), default));

        var claims = new ContentSetTestClaims();
        var claim = new Claim { TenantId = TenantA, ClaimCode = "CL-1", Status = ClaimStatuses.InReview };
        var claimInstance = Guid.NewGuid();
        claim.ReviewRounds.Add(new ClaimReviewRound { WorkflowInstanceId = claimInstance, RoundNo = 1, SubmittedAt = DateTimeOffset.UtcNow });
        claims.Items.Add(claim);
        var consumer = new ClaimWorkflowOutcomeConsumer(new ClaimReviewOutcomeApplier(claims, new EmptyCountryVersionRepo()),
            new Inbox(), NullLogger<ClaimWorkflowOutcomeConsumer>.Instance, regulatoryApplier: fx.Applier);

        await consumer.ConsumeAsync(Completed("crm.safety-text", safety.Id, fx.Instance(safety.Id), ClaimReviewOutcomes.Approved));
        await consumer.ConsumeAsync(Completed("crm.country-legal-profile", profile.Id, fx.Instance(profile.Id), ClaimReviewOutcomes.Rejected));
        await consumer.ConsumeAsync(Completed(ClaimReviewRules.ClaimObjectType, claim.Id, claimInstance, ClaimReviewOutcomes.Approved));

        Assert.Equal(RegulatoryTextStatuses.Active, fx.SafetyRepo.Items.Single().Status);
        Assert.Equal(RegulatoryTextStatuses.Draft, fx.LegalRepo.Items.Single().Status);
        Assert.Equal(ClaimStatuses.Approved, claim.Status);                        // the claim route is unchanged

        // A forged / replayed instance id never lands.
        Assert.Equal(ClaimReviewApplyResult.NoMatchingOpenRound, await fx.Applier.ApplyAsync(TenantA,
            "crm.safety-text", safety.Id, Guid.NewGuid(), ClaimReviewOutcomes.Rejected, "x", null, DateTimeOffset.UtcNow, default));
    }

    [Fact]
    public async Task An_old_open_round_is_reconciled_on_read()
    {
        var fx = new Fixture();
        var draft = await fx.SubmittedSafetyAsync();
        fx.SafetyRepo.Items.Single().ReviewRounds.Single().SubmittedAt = DateTimeOffset.UtcNow.AddHours(-1);
        fx.Workflow.Complete(fx.Instance(draft.Id), ClaimReviewOutcomes.Approved);

        var read = Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default));

        Assert.Equal(RegulatoryTextStatuses.Active, read.Status);
        Assert.Equal("crm.safety-text", fx.Workflow.ByObjectsCalls.Single().ObjectType);
    }

    // ================================================================ resolve / tenant

    [Fact]
    public async Task Resolve_returns_only_the_active_text_of_exactly_that_key()
    {
        var fx = new Fixture();
        Code(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "TR", "tr"), default), 404,
            RegulatoryTextErrors.SafetyTextMissing);
        await fx.CreateSafetyAsync();
        Code(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "TR", "tr"), default), 404,
            RegulatoryTextErrors.SafetyTextMissing);                              // a draft never resolves

        await fx.ActiveSafetyAsync(country: "UZ", language: "uz");
        Code(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "UZ", "ru"), default), 404,
            RegulatoryTextErrors.SafetyTextMissing);
        Code(await fx.Safety().Handle(new ResolveSafetyTextQuery(Guid.NewGuid(), "UZ", "uz"), default), 404,
            RegulatoryTextErrors.SafetyTextMissing);
        Assert.Equal("uz", Ok(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "uz", "UZ"), default)).LanguageCode);
    }

    [Fact]
    public async Task A_legal_profile_follows_the_same_lifecycle_and_resolves_by_country_and_language()
    {
        var fx = new Fixture();
        Code(await fx.Legal().Handle(new ResolveCountryLegalProfileQuery("TR", "tr"), default), 404,
            RegulatoryTextErrors.LegalProfileMissing);

        var draft = Ok(await fx.Legal().Handle(fx.CreateLegal(), default));
        Assert.Equal("LGL-TR-0001", draft.CountryLegalProfileCode);
        Code(await fx.Legal().Handle(fx.CreateLegal(), default), 409, RegulatoryTextErrors.LegalProfileOpenDraftExists);
        Ok(await fx.Legal().Handle(new SubmitCountryLegalProfileCommand(draft.Id), default));
        Assert.Equal(("KP-REG-TR", "crm.country-legal-profile", $"/CRM/LegalProfiles/{draft.Id:D}"),
            (fx.Workflow.Starts.Single().TemplateCode, fx.Workflow.Starts.Single().ObjectType,
                fx.Workflow.Starts.Single().DisplayContext.DeepLinkUrl));
        await fx.ApplyAsync(RegulatoryTextKind.LegalProfile, draft.Id, ClaimReviewOutcomes.Approved);

        var resolved = Ok(await fx.Legal().Handle(new ResolveCountryLegalProfileQuery("tr", "TR"), default));
        Assert.Equal(("Ruhsat sahibi: X İlaç A.Ş.", "{CC}-{YYYY}-{SEQ}"),
            (resolved.MarketingAuthorizationHolder, resolved.PageApprovalCodeFormat));
        Code(await fx.Legal().Handle(new ResolveCountryLegalProfileQuery("GB", "en"), default), 404,
            RegulatoryTextErrors.LegalProfileMissing);
    }

    [Fact]
    public async Task Another_tenant_sees_nothing()
    {
        var fx = new Fixture();
        var active = await fx.ActiveSafetyAsync();

        fx.Tenant.SetTenant(TenantB);
        Assert.Equal(404, (await fx.Safety().Handle(new GetSafetyTextQuery(active.Id), default)).StatusCode);
        Assert.Empty(Ok(await fx.Safety().Handle(new ListSafetyTextsQuery(), default)));
        Code(await fx.Safety().Handle(new ResolveSafetyTextQuery(ProductX, "TR", "tr"), default), 404,
            RegulatoryTextErrors.SafetyTextMissing);
        Assert.Equal(404, (await fx.Safety().Handle(new SubmitSafetyTextCommand(active.Id), default)).StatusCode);
    }

    // ================================================================ validation

    [Fact]
    public async Task The_product_is_checked_against_mdm_fail_closed()
    {
        var fx = new Fixture();
        fx.Products.Outcome = IStrategyTemplateProductReferenceValidator.Outcome.Unavailable;
        Code(await fx.Safety().Handle(fx.CreateSafety(), default), 503, RegulatoryTextErrors.DependencyUnavailable);

        fx.Products.Outcome = IStrategyTemplateProductReferenceValidator.Outcome.NotFound;
        Code(await fx.Safety().Handle(fx.CreateSafety(), default), 400, RegulatoryTextErrors.ProductNotFound);

        var noValidator = new Fixture { NoProductValidator = true };
        Code(await noValidator.Safety().Handle(noValidator.CreateSafety(), default), 503, RegulatoryTextErrors.DependencyUnavailable);
        Assert.Empty(fx.SafetyRepo.Items);
        Assert.Equal(IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct, fx.Products.Asked.Last().Kind);
    }

    [Fact]
    public async Task Country_and_language_go_through_the_kp1_validator()
    {
        var fx = new Fixture();
        Code(await fx.Safety().Handle(fx.CreateSafety(country: "XX"), default), 400, ChainContextErrors.CountryInvalid);
        Code(await fx.Safety().Handle(fx.CreateSafety(language: "en"), default), 400, ChainContextErrors.LanguageNotInCountry);
        Code(await fx.Legal().Handle(fx.CreateLegal(language: "en"), default), 400, ChainContextErrors.LanguageNotInCountry);

        fx.Catalog.Published = false;
        Code(await fx.Safety().Handle(fx.CreateSafety(), default), 503, ChainContextErrors.ReferenceSetUnavailable);
        Assert.Empty(fx.SafetyRepo.Items);
        Assert.Empty(fx.LegalRepo.Items);
    }

    [Fact]
    public async Task Texts_are_required_and_bounded()
    {
        var fx = new Fixture();
        Code(await fx.Safety().Handle(fx.CreateSafety(body: "   "), default), 400, RegulatoryTextErrors.TextRequired);
        Code(await fx.Safety().Handle(fx.CreateSafety(body: new string('x', RegulatoryTextLimits.SafetyBody + 1)), default), 400,
            RegulatoryTextErrors.TextTooLong);
        Code(await fx.Safety().Handle(fx.CreateSafety() with { ShortBody = new string('x', RegulatoryTextLimits.SafetyShortBody + 1) },
            default), 400, RegulatoryTextErrors.TextTooLong);
        Code(await fx.Legal().Handle(fx.CreateLegal() with { LegalFooterText = null }, default), 400, RegulatoryTextErrors.TextRequired);
        Assert.Equal(201, (await fx.Safety().Handle(fx.CreateSafety(body: new string('x', RegulatoryTextLimits.SafetyBody)), default))
            .StatusCode);
    }

    // ================================================================ persistence shape

    [Fact]
    public void Class_maps_store_guids_as_strings_and_round_trip_without_a_discriminator()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var instance = Guid.NewGuid();
        var text = new SafetyText
        {
            TenantId = TenantA, Code = "SAF-TR-0001", GlobalProductId = ProductX, CountryCode = "TR", LanguageCode = "tr",
            VersionNumber = 3, Status = RegulatoryTextStatuses.Active, Body = Body, ShortBody = "Kısa",
            ReviewRounds = { new ClaimReviewRound { WorkflowInstanceId = instance, RoundNo = 1, SubmittedAt = DateTimeOffset.UtcNow } },
            Decisions = { new RegulatoryTextDecision { RoundNo = 1, By = Regulator, Outcome = "approve", At = DateTimeOffset.UtcNow } }
        };

        var doc = text.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["GlobalProductId"].BsonType);
        Assert.Equal(BsonType.String, doc["ReviewRounds"].AsBsonArray[0]["WorkflowInstanceId"].BsonType);
        Assert.False(doc.Contains("_t"));
        var back = BsonSerializer.Deserialize<SafetyText>(doc);
        Assert.Equal((ProductX, 3, Body, instance, Regulator),
            (back.GlobalProductId, back.VersionNumber, back.Body, back.ReviewRounds[0].WorkflowInstanceId, back.Decisions[0].By));

        var profile = new CountryLegalProfile
        {
            TenantId = TenantA, CountryCode = "TR", LanguageCode = "tr", LegalFooterText = "Yasal", PageApprovalCodeFormat = "{CC}"
        };
        var profileBack = BsonSerializer.Deserialize<CountryLegalProfile>(profile.ToBsonDocument());
        Assert.Equal(("Yasal", "{CC}"), (profileBack.LegalFooterText, profileBack.PageApprovalCodeFormat));
    }

    [Fact]
    public void One_active_per_key_is_a_unique_partial_index_with_an_equality_filter()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var registry = BsonSerializer.SerializerRegistry;

        var safety = SafetyTextRepository.ActiveKeyIndex();
        var safetyOptions = (MongoDB.Driver.CreateIndexOptions<SafetyText>)safety.Options;
        Assert.True(safetyOptions.Unique);
        Assert.Equal(new[] { "TenantId", "GlobalProductId", "CountryCode", "LanguageCode" },
            safety.Keys.Render(registry.GetSerializer<SafetyText>(), registry).Names);
        Assert.Equal(new BsonDocument { { "Status", "active" }, { "IsDeleted", false } },
            safetyOptions.PartialFilterExpression.Render(registry.GetSerializer<SafetyText>(), registry));

        var legal = CountryLegalProfileRepository.ActiveKeyIndex();
        var legalOptions = (MongoDB.Driver.CreateIndexOptions<CountryLegalProfile>)legal.Options;
        Assert.True(legalOptions.Unique);
        Assert.Equal(new[] { "TenantId", "CountryCode", "LanguageCode" },
            legal.Keys.Render(registry.GetSerializer<CountryLegalProfile>(), registry).Names);
        Assert.DoesNotContain("$ne", legalOptions.PartialFilterExpression
            .Render(registry.GetSerializer<CountryLegalProfile>(), registry).ToString());
    }

    [Fact]
    public void One_open_version_per_key_is_a_unique_partial_index_on_the_derived_open_key()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var registry = BsonSerializer.SerializerRegistry;
        foreach (var (keys, filter) in new[]
                 {
                     Render(SafetyTextRepository.OpenKeyIndex(SafetyTextRepository.OpenKeyIndexName), registry),
                     Render(CountryLegalProfileRepository.OpenKeyIndex(CountryLegalProfileRepository.OpenKeyIndexName), registry)
                 })
        {
            Assert.Equal(new[] { "TenantId", "OpenKey" }, keys);
            Assert.Equal(new BsonDocument { { "OpenKey", new BsonDocument("$type", 2) }, { "IsDeleted", false } }, filter);
            Assert.DoesNotContain("$ne", filter.ToString());
            Assert.DoesNotContain("$in", filter.ToString());
        }
    }

    [Fact]
    public void The_open_key_is_derived_from_the_state_and_stored_for_the_index_only()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var text = new SafetyText { TenantId = TenantA, GlobalProductId = ProductX, CountryCode = "TR", LanguageCode = "tr", Body = Body };
        Assert.Equal(text.Key(), text.OpenKey);                                // draft = open
        text.Status = RegulatoryTextStatuses.InReview;
        Assert.Equal(text.Key(), text.OpenKey);                                // in review = open
        foreach (var closed in new[] { RegulatoryTextStatuses.Active, RegulatoryTextStatuses.Superseded, RegulatoryTextStatuses.Archived })
        {
            text.Status = closed;
            Assert.Null(text.OpenKey);
        }

        // Stored as a string while open (indexed), as null when closed (outside the partial filter); a stale stored value
        // never wins on read — the state does.
        text.Status = RegulatoryTextStatuses.Draft;
        var doc = text.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["OpenKey"].BsonType);
        doc["Status"] = RegulatoryTextStatuses.Active;
        Assert.Null(BsonSerializer.Deserialize<SafetyText>(doc).OpenKey);
    }

    [Fact]
    public void The_read_model_carries_the_names_the_web_screens_read()
    {
        var dto = RegulatoryTextMapper.ToDto(
            new SafetyText
            {
                TenantId = TenantA, Code = "SAF-TR-0001", GlobalProductId = ProductX, GlobalProductCodeDisplay = "GP-X",
                CountryCode = "TR", LanguageCode = "tr", VersionNumber = 2, Body = Body,
                Decisions = { new RegulatoryTextDecision { RoundNo = 1, By = Regulator, Outcome = "reject", Comment = "Eksik", At = DateTimeOffset.UtcNow } }
            },
            new RegulatoryTextAbilities(true, true, false));
        var json = System.Text.Json.JsonSerializer.SerializeToElement(dto, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        foreach (var name in new[] { "id", "safetyTextCode", "globalProductId", "globalProductCodeDisplay", "countryCode", "languageCode",
                     "version", "status", "isActive", "body", "shortBody", "sourceDocumentRef", "sourceDate", "approvalReference",
                     "canEdit", "canSubmit", "canDecide", "decisions", "workflowInstanceId", "reviewLink", "submittedBy", "updatedAt" })
        {
            Assert.True(json.TryGetProperty(name, out _), $"missing '{name}'");
        }

        Assert.Equal(2, json.GetProperty("version").GetInt32());
        Assert.Equal("draft", json.GetProperty("status").GetString());
        var decision = json.GetProperty("decisions")[0];
        Assert.Equal(("reject", "Eksik", Regulator), (decision.GetProperty("outcome").GetString(), decision.GetProperty("comment").GetString(), decision.GetProperty("by").GetString()));
        Assert.True(decision.TryGetProperty("at", out _));

        var profile = System.Text.Json.JsonSerializer.SerializeToElement(
            RegulatoryTextMapper.ToDto(new CountryLegalProfile { Code = "LGL-TR-0001", CountryCode = "TR", LanguageCode = "tr", LegalFooterText = "Yasal" },
                new RegulatoryTextAbilities(false, false, false)),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        foreach (var name in new[] { "id", "countryLegalProfileCode", "legalFooterText", "marketingAuthorizationHolder",
                     "adverseEventReportingText", "promotionalNotice", "pageApprovalCodeFormat", "version", "status" })
        {
            Assert.True(profile.TryGetProperty(name, out _), $"missing '{name}'");
        }
    }

    private static (string[] Keys, BsonDocument Filter) Render<T>(MongoDB.Driver.CreateIndexModel<T> index, IBsonSerializerRegistry registry)
    {
        var options = (MongoDB.Driver.CreateIndexOptions<T>)index.Options;
        Assert.True(options.Unique);
        return (index.Keys.Render(registry.GetSerializer<T>(), registry).Names.ToArray(),
            options.PartialFilterExpression.Render(registry.GetSerializer<T>(), registry));
    }

    // ================================================================ WP-KP-5a-FIX-1 (E4 findings)

    [Fact]
    public async Task Each_kind_starts_its_round_with_its_own_reason_code()
    {
        var fx = new Fixture();
        var text = await fx.SubmittedSafetyAsync();
        var profile = Ok(await fx.Legal().Handle(fx.CreateLegal(), default));
        Ok(await fx.Legal().Handle(new SubmitCountryLegalProfileCommand(profile.Id), default));

        Assert.Equal(new[] { "CRM_SAFETY_TEXT_SUBMITTED", "CRM_COUNTRY_LEGAL_PROFILE_SUBMITTED" },
            fx.Workflow.Starts.Select(s => s.ReasonCode).ToArray());
        Assert.Equal(text.Id.ToString("D"), fx.Workflow.Starts[0].ObjectId);
    }

    [Fact]
    public async Task The_detail_names_its_people_in_one_bulk_call_and_an_unknown_id_stays_unnamed()
    {
        var author = Guid.NewGuid();
        var regulator = Guid.NewGuid();
        var stranger = Guid.NewGuid();   // e.g. a user of another tenant: the resolver never answers it
        var fx = new Fixture();
        fx.Names.Known[author] = "Ayşe Yazar";
        fx.Names.Known[regulator] = "sema pullukcu";

        fx.Actor.Name = author.ToString("D");
        var draft = Ok(await fx.Safety().Handle(fx.CreateSafety(), default));
        Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default));
        fx.Actor.Name = regulator.ToString("D");
        var task = fx.Workflow.Tasks.Last(t => t.WorkflowInstanceId == fx.Instance(draft.Id));
        fx.Decisions.Mine.Add(task);
        Ok(await fx.Safety().Handle(new DecideSafetyTextCommand(draft.Id, "reject", "Eksik."), default));
        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, draft.Id, ClaimReviewOutcomes.Rejected);
        // A Work Center decider the resolver cannot name (and a non-id system actor) on a later round.
        fx.SafetyRepo.Items.Single(t => t.Id == draft.Id).Decisions.Add(
            new RegulatoryTextDecision { RoundNo = 9, By = stranger.ToString("D"), Outcome = "approve", At = DateTimeOffset.UtcNow });
        fx.Names.Calls.Clear();

        var detail = Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default));

        var call = Assert.Single(fx.Names.Calls);                               // ONE bulk call
        Assert.Equal(new[] { author, regulator, stranger }.OrderBy(g => g), call.OrderBy(g => g));
        Assert.Equal(("Ayşe Yazar", "Ayşe Yazar"), (detail.CreatedByName, detail.SubmittedByName));
        Assert.Equal("sema pullukcu", detail.Decisions.Single(d => d.RoundNo == 1).ByName);
        var unknown = detail.Decisions.Single(d => d.RoundNo == 9);
        Assert.Null(unknown.ByName);                                             // never the id, never a guess
        Assert.Equal(stranger.ToString("D"), unknown.By);
    }

    [Fact]
    public async Task Can_archive_follows_the_archive_rule_of_the_command()
    {
        var fx = new Fixture();
        var draft = await fx.CreateSafetyAsync();
        Assert.True(draft.CanArchive);                                                            // draft
        Assert.True(Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default)).CanArchive);

        var inReview = Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default));
        Assert.False(inReview.CanArchive);                                                         // in review
        Code(await fx.Safety().Handle(new ArchiveSafetyTextCommand(draft.Id), default), 409, RegulatoryTextErrors.NotEditable);

        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, draft.Id, ClaimReviewOutcomes.Approved);
        var active = Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default));
        Assert.True(active.CanArchive);                                                            // active

        var v2 = Ok(await fx.Safety().Handle(new NewSafetyTextVersionCommand(draft.Id), default));
        Ok(await fx.Safety().Handle(new SubmitSafetyTextCommand(v2.Id), default));
        await fx.ApplyAsync(RegulatoryTextKind.SafetyText, v2.Id, ClaimReviewOutcomes.Approved);
        Assert.True(Ok(await fx.Safety().Handle(new GetSafetyTextQuery(draft.Id), default)).CanArchive);   // superseded

        var archived = Ok(await fx.Safety().Handle(new ArchiveSafetyTextCommand(v2.Id), default));
        Assert.False(archived.CanArchive);                                                         // archived
    }

    [Fact]
    public async Task A_draft_lost_to_a_concurrent_writer_is_409_not_500()
    {
        var fx = new Fixture();
        fx.SafetyRepo.ConflictOnNextInsert = true;     // the store's unique open-key index refuses the insert
        Code(await fx.Safety().Handle(fx.CreateSafety(), default), 409, RegulatoryTextErrors.SafetyTextOpenDraftExists);

        fx.LegalRepo.ConflictOnNextInsert = true;
        Code(await fx.Legal().Handle(fx.CreateLegal(), default), 409, RegulatoryTextErrors.LegalProfileOpenDraftExists);

        // The same for a new version racing another one.
        var active = await fx.ActiveSafetyAsync();
        fx.SafetyRepo.ConflictOnNextInsert = true;
        Code(await fx.Safety().Handle(new NewSafetyTextVersionCommand(active.Id), default), 409,
            RegulatoryTextErrors.SafetyTextOpenDraftExists);
    }

    [Fact]
    public void The_safety_text_is_not_a_claim()
        => Assert.False(typeof(Claim).IsAssignableFrom(typeof(SafetyText))); // K2

    // ================================================================ helpers

    private static T Ok<T>(Response<T> response)
    {
        Assert.True(response.IsSuccessful, string.Join(" | ", response.Errors ?? new List<string>()));
        return response.Data!;
    }

    private static void Code<T>(Response<T> response, int status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, response.Errors!);
    }

    private static EventTransportMessage Completed(string objectType, Guid objectId, Guid instanceId, string outcome)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(), tenantId = TenantA, workflowInstanceId = instanceId, templateCode = "KP-REG-TR",
            objectType, objectId = objectId.ToString("D"), objectRef = "x", outcome, completedAt = DateTimeOffset.UtcNow,
            completedBy = Regulator, reasonCode = "X"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EventTransportMessage(Guid.NewGuid(), ClaimWorkflowOutcomeConsumer.CompletedEventName, 1, Guid.NewGuid(),
            null, TenantA, "Diten.Platform", DateTimeOffset.UtcNow, payload);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Tenant.SetTenant(TenantA);
            Applier = new RegulatoryTextOutcomeApplier(SafetyRepo, LegalRepo);
        }

        public TenantContext Tenant { get; } = new();
        public ActorBox Actor { get; } = new() { Name = Author };
        public SafetyRepository SafetyRepo { get; } = new();
        public LegalRepository LegalRepo { get; } = new();
        public FakeClaimWorkflowClient Workflow { get; } = new();
        public DecisionClient Decisions { get; } = new();
        public ContentSetTestCatalog Catalog { get; } = new();
        public ProductValidator Products { get; } = new();
        public NameResolver Names { get; } = new();
        public RegulatoryTextOutcomeApplier Applier { get; }
        public IRegulatoryTextReviewSettings? Settings { get; init; }
        public bool NoProductValidator { get; init; }

        private RegulatoryTextReviewReconciler Reconciler() => new(Workflow, Applier, Settings);

        public SafetyTextHandlers Safety() => new(Tenant, Actor, SafetyRepo, Workflow, Decisions, Applier, Reconciler(), Settings,
            Catalog, NoProductValidator ? null : Products, Names);

        public CountryLegalProfileHandlers Legal() => new(Tenant, Actor, LegalRepo, Workflow, Decisions, Applier, Reconciler(),
            Settings, Catalog, Names);

        public CreateSafetyTextCommand CreateSafety(string country = "TR", string language = "tr", string? body = Body)
            => new(ProductX, "ALMIBA", country, language, body, null, "KÜB 2026-01", null, "TR-2026/14");

        public CreateCountryLegalProfileCommand CreateLegal(string country = "TR", string language = "tr")
            => new(country, language, "Bu materyal sağlık mesleği mensuplarına yöneliktir.", "Ruhsat sahibi: X İlaç A.Ş.",
                "Şüpheli advers reaksiyonları TÜFAM'a bildiriniz.", null, "{CC}-{YYYY}-{SEQ}");

        public Guid Instance(Guid id)
            => (SafetyRepo.Items.FirstOrDefault(t => t.Id == id)?.OpenRound()
                ?? LegalRepo.Items.First(p => p.Id == id).OpenRound())!.WorkflowInstanceId;

        public async Task<SafetyTextDto> CreateSafetyAsync(string country = "TR", string language = "tr")
        {
            Actor.Name = Author;
            return Ok(await Safety().Handle(CreateSafety(country, language), default));
        }

        public async Task<SafetyTextDto> SubmittedSafetyAsync(string country = "TR", string language = "tr")
        {
            var draft = await CreateSafetyAsync(country, language);
            return Ok(await Safety().Handle(new SubmitSafetyTextCommand(draft.Id), default));
        }

        public async Task<SafetyTextDto> ActiveSafetyAsync(string country = "TR", string language = "tr")
        {
            var submitted = await SubmittedSafetyAsync(country, language);
            await ApplyAsync(RegulatoryTextKind.SafetyText, submitted.Id, ClaimReviewOutcomes.Approved);
            return Ok(await Safety().Handle(new GetSafetyTextQuery(submitted.Id), default));
        }

        /// <summary>The Regulatory reviewer decides through CRM on the round's open task.</summary>
        public async Task<SafetyTextDto> DecideAsync(Guid id, string decision, string? comment)
        {
            Actor.Name = Regulator;
            var task = Workflow.Tasks.Last(t => t.WorkflowInstanceId == Instance(id));
            Decisions.Mine.Add(task);
            return Ok(await Safety().Handle(new DecideSafetyTextCommand(id, decision, comment), default));
        }

        /// <summary>What the completion event does (the consumer's applier call).</summary>
        public async Task ApplyAsync(RegulatoryTextKind kind, Guid id, string outcome)
            => Assert.Equal(ClaimReviewApplyResult.Applied, await Applier.ApplyAsync(TenantA, kind.ObjectType, id, Instance(id),
                outcome, Regulator, "X", DateTimeOffset.UtcNow, default));
    }

    private sealed class ActorBox : IActorContext
    {
        public string? Name { get; set; }
        public string? ActorName => Name;
    }

    private sealed class Inbox : ICrmEventInboxRepository
    {
        private readonly HashSet<Guid> _seen = new();

        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_seen.Add(eventId));
    }

    private sealed class DecisionClient : IWorkflowDecisionClient
    {
        public List<ClaimWorkflowTaskState> Mine { get; } = new();
        public List<(Guid TaskId, bool Approve, string Actor, string? Comment)> Calls { get; } = new();

        public Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ClaimWorkflowTaskState>?>(Mine.ToList());

        public Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode,
            string idempotencyKey, string? comment, CancellationToken ct)
        {
            Calls.Add((taskId, approve, actorId, comment));
            return Task.FromResult(new WorkflowDecisionResult(ClaimWorkflowCallOutcome.Ok, null));
        }

        public Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<WorkflowHistoryEntry>?>(Array.Empty<WorkflowHistoryEntry>());
    }

    /// <summary>WP-KP-5a-FIX-1 — the tenant-scoped display-name seam: answers only the ids it knows, records each call.</summary>
    private sealed class NameResolver : IUserDisplayNameResolver
    {
        public Dictionary<Guid, string> Known { get; } = new();
        public List<IReadOnlyCollection<Guid>> Calls { get; } = new();

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            Calls.Add(userIds.ToList());
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                userIds.Where(Known.ContainsKey).ToDictionary(id => id, id => Known[id]));
        }
    }

    private sealed class ProductValidator : IStrategyTemplateProductReferenceValidator
    {
        public IStrategyTemplateProductReferenceValidator.Outcome Outcome { get; set; } =
            IStrategyTemplateProductReferenceValidator.Outcome.Valid;

        public List<(string Kind, Guid Id)> Asked { get; } = new();

        public Task<IStrategyTemplateProductReferenceValidator.Outcome> ValidateAsync(
            string referenceKind, Guid referenceId, CancellationToken cancellationToken)
        {
            Asked.Add((referenceKind, referenceId));
            return Task.FromResult(Outcome);
        }
    }

    /// <summary>In-memory store with the production version-check semantics (and NO unique index — the tests prove the
    /// single-active rule in the application, the index separately).</summary>
    private class MemoryRepo<T> : IRegulatoryTextRepository<T> where T : RegulatoryText
    {
        public List<T> Items { get; } = new();

        public Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<T>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList());

        /// <summary>WP-KP-5a-FIX-1 — the next insert loses the race on the store's unique open-key index.</summary>
        public bool ConflictOnNextInsert { get; set; }

        public Task InsertAsync(T entity, CancellationToken ct)
        {
            if (ConflictOnNextInsert)
            {
                ConflictOnNextInsert = false;
                throw new RegulatoryTextKeyConflictException("duplicate key (open version of the key)");
            }

            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(T entity, int expectedVersion, CancellationToken ct)
        {
            var index = Items.FindIndex(x => x.Id == entity.Id && x.TenantId == entity.TenantId);
            if (index < 0 || Items[index].Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            entity.Version = expectedVersion + 1;
            Items[index] = entity;
            return Task.FromResult(true);
        }
    }

    private sealed class SafetyRepository : MemoryRepo<SafetyText>, ISafetyTextRepository;

    private sealed class LegalRepository : MemoryRepo<CountryLegalProfile>, ICountryLegalProfileRepository;
}
