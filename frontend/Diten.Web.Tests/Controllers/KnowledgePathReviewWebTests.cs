using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.KnowledgePaths;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using R = Diten.Web.Controllers.CRM.KnowledgePathStudioReview;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-KP-UI-2 — the studio review surface on the Web layer: the KP-2 / KP-3 proxy allowlist + permissions, the archive
/// PDF stream guard (bodiless / empty / 404 pass-through), the reviewer page route + its UAS-001 gate, the PERSON-based
/// hiding of the decision panel (submitter ↔ candidate, from the session identity exactly as CRM reads it), the client
/// rejection-comment check, every KP-2 / KP-3 error code as user text (anchored to the CRM constants), the wizard's
/// "decide every suggestion" gate, the 7-language family + JS ↔ resx parity for the new scripts, the compliance /
/// MLR / diff / release-precondition rules and that no raw code reaches a label. Labels come from the REAL resx.
/// </summary>
public sealed class KnowledgePathReviewWebTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid PathId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SubjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ChainId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TypeNeed = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid TypeFatigue = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid ContentTr = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid ContentDraft = Guid.Parse("70000000-0000-0000-0000-000000000003");
    private static readonly Guid ContentEn = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid StepA = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid StepB = Guid.Parse("80000000-0000-0000-0000-000000000002");
    private static readonly Guid StepLoose = Guid.Parse("80000000-0000-0000-0000-000000000003");
    private static readonly Guid ClaimOk = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid ClaimDraft = Guid.Parse("90000000-0000-0000-0000-000000000002");
    private static readonly Guid VersionOk = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid Rev1 = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid Rev2 = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid Instance2 = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid NoteId = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private const string Read = "crm.knowledge.path.read";
    private const string Manage = "crm.knowledge.path.manage";
    private const string Publish = "crm.knowledge.path.publish";
    private const string Author = "user-author";
    private const string Reviewer = "user-legal";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ============================================================ reviewer page (route + UAS-001)

    [Fact]
    public void The_reviewer_page_is_the_work_center_link_route_and_a_plain_403_without_read()
    {
        var action = typeof(KnowledgePathsController).GetMethod(nameof(KnowledgePathsController.Review))!;
        Assert.Equal("{pathId:guid}/Review/{revisionId:guid}", Assert.Single(action.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Equal("CRM/KnowledgePaths", typeof(KnowledgePathsController).GetCustomAttribute<RouteAttribute>()!.Template);

        var denied = Controller(new StubGateway(), Reviewer).Review(PathId, Rev2);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(denied).StatusCode);   // no skeleton, no redirect

        var view = Assert.IsType<ViewResult>(Controller(new StubGateway(), Reviewer, Read).Review(PathId, Rev2));
        Assert.EndsWith("Review.cshtml", view.ViewName);
        Assert.Equal((PathId.ToString(), Rev2.ToString()), (view.ViewData["PathId"], view.ViewData["RevisionId"]));
    }

    [Fact]
    public async Task Every_review_read_is_403_without_read()
    {
        var controller = Controller(new StubGateway(), Reviewer);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.ReviewerView(PathId, Rev2, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.StudioReview(PathId, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.RevisionArtifact(PathId, Rev2, null, false, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.StudioUsage(PathId, default)).StatusCode);
    }

    // ============================================================ person-based decision hiding (D-KP-8)

    [Fact]
    public async Task The_submitter_never_gets_the_decision_panel_person_based()
    {
        using var _ = new UiCulture("tr");
        var model = Data(await Controller(new StubGateway(), Author.ToUpperInvariant(), Read, Manage).ReviewerView(PathId, Rev2, default));
        Assert.True(model.GetProperty("isSubmitter").GetBoolean());
        Assert.False(model.GetProperty("canDecide").GetBoolean());
        Assert.Equal("Bu revizyonu siz gönderdiniz. Gönderen kendi yolunu onaylayamaz.", model.GetProperty("decisionNote").GetString());
        Assert.Equal("Siz", model.GetProperty("submittedBy").GetString());       // never the id
    }

    [Fact]
    public async Task A_candidate_who_is_not_the_submitter_gets_the_decision_panel_with_the_step_name()
    {
        using var _ = new UiCulture("tr");
        var model = Data(await Controller(new StubGateway(), Reviewer, Read).ReviewerView(PathId, Rev2, default));
        Assert.False(model.GetProperty("isSubmitter").GetBoolean());
        Assert.True(model.GetProperty("canDecide").GetBoolean());
        Assert.Equal("Hukuk inceleme", model.GetProperty("currentStepName").GetString());
        Assert.Equal(JsonValueKind.Null, model.GetProperty("decisionNote").ValueKind);
        Assert.Equal("Ayşe Kaya", model.GetProperty("submittedBy").GetString());  // MOD-0023 display name, not the id
    }

    [Fact]
    public async Task The_round_submitter_is_the_submitter_even_when_another_user_created_the_revision()
    {
        var gateway = new StubGateway { CreatedBy = "someone-else" };
        var model = Data(await Controller(gateway, Author, Read).ReviewerView(PathId, Rev2, default));
        Assert.True(model.GetProperty("isSubmitter").GetBoolean());
        Assert.False(model.GetProperty("canDecide").GetBoolean());
    }

    [Fact]
    public void The_session_actor_is_read_as_crm_reads_it()
    {
        static ClaimsPrincipal User(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
        Assert.Equal("sub-1", R.ActorOf(User(new Claim("sub", "sub-1"), new Claim(ClaimTypes.NameIdentifier, "nid"))));
        Assert.Equal("nid", R.ActorOf(User(new Claim(ClaimTypes.NameIdentifier, "nid"), new Claim("email", "a@b"))));
        Assert.Equal("a@b", R.ActorOf(User(new Claim("email", "a@b"))));
        Assert.Null(R.ActorOf(new ClaimsPrincipal(new ClaimsIdentity())));    // anonymous → no actor, never decides
        Assert.True(R.SamePerson(" Sub-1 ", "sub-1"));
        Assert.False(R.SamePerson(null, null));
    }

    [Fact]
    public void The_client_hides_the_decision_form_unless_the_server_allowed_it_and_checks_the_reject_comment()
    {
        var script = Script("review.js");
        Assert.Contains("const decisionAllowed = m => !!m && m.canDecide === true && m.isSubmitter !== true && m.roundOpen === true;", script);
        Assert.Contains("$('rvDecisionForm').classList.toggle('d-none', !allowed);", script);
        Assert.Contains("const rejectCommentMissing = (decision, comment) => decision === 'reject' && !String(comment || '').trim();", script);
        // In decide(): the comment check returns BEFORE the decision is posted.
        var decide = Regex.Match(script, @"const decide = async decision => \{(?<b>.*?)\n    \};", RegexOptions.Singleline).Groups["b"].Value;
        var check = decide.IndexOf("if (rejectCommentMissing(decision, comment))", StringComparison.Ordinal);
        var post = decide.IndexOf("api.post(", StringComparison.Ordinal);
        Assert.True(check >= 0 && post > check, "the reject-comment check must run before the decision POST");
        Assert.Matches(@"if \(rejectCommentMissing\(decision, comment\)\) \{[^}]*return;", decide);
    }

    // ============================================================ proxies

    [Fact]
    public async Task Review_proxies_forward_to_the_kp2_kp3_endpoints_with_the_right_permission()
    {
        var gateway = new StubGateway();
        var reader = Controller(gateway, Reviewer, Read);
        var body = JsonDocument.Parse("""{"decision":"reject","comment":"Yan etki satırı eksik."}""").RootElement;
        Assert.Equal(403, Assert.IsType<ObjectResult>(await reader.SubmitReview(PathId, default)).StatusCode);       // manage
        Assert.Equal(403, Assert.IsType<ObjectResult>(await reader.RenderRevision(PathId, Rev2, default)).StatusCode); // publish
        Assert.Equal(403, Assert.IsType<ObjectResult>(await reader.ReleaseRevision(PathId, Rev2, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Controller(gateway, Author, Read, Manage)
            .WithdrawRelease(PathId, Rev2, JsonDocument.Parse("""{"reason":"x"}""").RootElement, default)).StatusCode); // manage ≠ publish

        await reader.Decide(PathId, Rev2, body, default);                            // read is enough: MOD-0023 decides
        await reader.AddNote(PathId, Rev2, JsonDocument.Parse("""{"stepRef":"BR1:x","text":"n"}""").RootElement, default);
        await reader.ResolveNote(PathId, Rev2, NoteId, default);
        var manager = Controller(gateway, Author, Read, Manage);
        await manager.SubmitReview(PathId, default);
        await manager.WithdrawReview(PathId, default);
        var publisher = Controller(gateway, Reviewer, Read, Publish);
        await publisher.RenderRevision(PathId, Rev2, default);
        await publisher.ReleaseRevision(PathId, Rev2, default);
        await publisher.WithdrawRelease(PathId, Rev2, JsonDocument.Parse("""{"reason":"Etiket"}""").RootElement, default);

        Assert.Equal(
        [
            $"POST /api/crm/knowledge/paths/{PathId}/revisions/{Rev2}/decision",
            $"POST /api/crm/knowledge/paths/{PathId}/revisions/{Rev2}/notes",
            $"POST /api/crm/knowledge/paths/{PathId}/revisions/{Rev2}/notes/{NoteId}/resolve",
            $"POST /api/crm/knowledge/paths/{PathId}/submit-review",
            $"POST /api/crm/knowledge/paths/{PathId}/withdraw-review",
            $"POST /api/crm/knowledge/paths/{PathId}/revisions/{Rev2}/render",
            $"POST /api/crm/knowledge/paths/{PathId}/revisions/{Rev2}/release",
            $"POST /api/crm/knowledge/paths/{PathId}/revisions/{Rev2}/withdraw"
        ], gateway.Writes.Select(w => w.Method + " " + new Uri(w.Url).AbsolutePath));
        Assert.Contains("Yan etki satırı eksik.", gateway.Writes[0].Body);   // the comment travels with the one-channel decision

        var tenant = await manager.Decide(PathId, Rev2, JsonDocument.Parse("""{"tenantId":"x"}""").RootElement, default);
        Assert.IsType<BadRequestObjectResult>(tenant);
    }

    [Fact]
    public async Task The_artifact_streams_the_pdf_inline_or_as_a_download()
    {
        var gateway = new StubGateway { Artifact = (200, Encoding.ASCII.GetBytes("%PDF-1.4 test"), "KP-A-v1.0-R2.pdf") };
        var inline = Assert.IsType<FileContentResult>(await Controller(gateway, Reader, Read).RevisionArtifact(PathId, Rev2, null, false, default));
        Assert.Equal("application/pdf", inline.ContentType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(inline.FileContents, 0, 4));
        Assert.True(string.IsNullOrEmpty(inline.FileDownloadName));          // inline → the embedded viewer
        Assert.Contains(gateway.Requests, r => r.EndsWith($"/revisions/{Rev2}/artifact?kind=pdf"));

        var download = Assert.IsType<FileContentResult>(await Controller(gateway, Reader, Read).RevisionArtifact(PathId, Rev2, "pdf", true, default));
        Assert.Equal("KP-A-v1.0-R2.pdf", download.FileDownloadName);
    }

    [Theory]
    [InlineData(204, true, false)]   // "success" without bytes → 404 artifact_missing (never an empty PDF / a 204 with a body)
    [InlineData(404, false, true)]   // CRM's coded 404 passes through with its body
    [InlineData(404, false, false)]  // a bodiless upstream failure stays bodiless
    public async Task The_artifact_guard_handles_bodiless_empty_and_failed_reads(int status, bool emptySuccess, bool withBody)
    {
        var body = withBody ? Encoding.UTF8.GetBytes("""{"errors":["Revision not found."]}""") : [];
        var gateway = new StubGateway { Artifact = (status, body, null) };
        var result = await Controller(gateway, Reader, Read).RevisionArtifact(PathId, Rev2, null, false, default);
        if (emptySuccess)
        {
            var missing = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("artifact_missing", JsonSerializer.Serialize(missing.Value));
        }
        else if (withBody)
        {
            var pass = Assert.IsType<ContentResult>(result);
            Assert.Equal((404, """{"errors":["Revision not found."]}"""), (pass.StatusCode, pass.Content));
        }
        else
        {
            Assert.Equal(404, Assert.IsType<StatusCodeResult>(result).StatusCode);
        }
    }

    [Fact]
    public async Task Only_the_pdf_kind_is_served_in_this_phase()
    {
        var gateway = new StubGateway();
        var result = await Controller(gateway, Reader, Read).RevisionArtifact(PathId, Rev2, "html", false, default);
        Assert.Contains("artifact_missing", JsonSerializer.Serialize(Assert.IsType<NotFoundObjectResult>(result).Value));
        Assert.DoesNotContain(gateway.Requests, r => r.Contains("/artifact"));
    }

    // ============================================================ review model (MLR timeline, cards, release, next step)

    [Fact]
    public async Task The_review_model_has_the_mlr_timeline_with_step_names_people_dates_comments_and_candidates()
    {
        using var _ = new UiCulture("tr");
        var model = Data(await Controller(new StubGateway(), Author, Read, Manage).StudioReview(PathId, default));
        var timeline = model.GetProperty("timeline");
        Assert.True(timeline.GetProperty("isOpen").GetBoolean());
        var steps = timeline.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["Medikal inceleme", "Hukuk inceleme", "Ruhsat inceleme"], steps.Select(s => s.GetProperty("stepName").GetString()!).ToArray());
        Assert.Equal(["approved", "pending", "queued"], steps.Select(s => s.GetProperty("state").GetString()!).ToArray());
        Assert.Equal(("Onayladı", "Dr. Mert Demir", "Mekanizma cümlesi uygun."),
            (steps[0].GetProperty("stateLabel").GetString(), steps[0].GetProperty("actor").GetString(), steps[0].GetProperty("comment").GetString()));
        Assert.NotEqual(JsonValueKind.Null, steps[0].GetProperty("at").ValueKind);
        Assert.Equal(["Hukuk Müdürü"], steps[1].GetProperty("candidates").EnumerateArray().Select(c => c.GetString()!).ToArray());
        Assert.Equal("İncelemede · Hukuk inceleme", model.GetProperty("statusDetail").GetString());

        var submit = model.GetProperty("submit");
        Assert.False(submit.GetProperty("canSubmit").GetBoolean());               // a round is open
        Assert.True(submit.GetProperty("canWithdrawReview").GetBoolean());
        Assert.Equal(3, submit.GetProperty("nextRevisionNumber").GetInt32());
        Assert.Equal("waiting", model.GetProperty("next").GetProperty("key").GetString());
        var tabs = model.GetProperty("tabs");
        Assert.True(tabs.GetProperty("mlr").GetBoolean());
        Assert.False(tabs.GetProperty("output").GetBoolean());                    // no publish permission
    }

    [Fact]
    public async Task An_approved_revision_offers_render_then_release_but_never_to_its_submitter()
    {
        using var _ = new UiCulture("tr");
        var approved = new StubGateway { Approved = true };
        var asSubmitter = Data(await Controller(approved, Author, Read, Manage, Publish).StudioReview(PathId, default));
        var candidate = asSubmitter.GetProperty("release").GetProperty("candidate");
        var pre = candidate.GetProperty("preconditions").EnumerateArray().ToDictionary(p => p.GetProperty("key").GetString()!);
        Assert.True(pre["approved"].GetProperty("ok").GetBoolean());
        Assert.True(pre["artifact"].GetProperty("ok").GetBoolean());
        Assert.False(pre["sod"].GetProperty("ok").GetBoolean());
        Assert.Equal("Yayınlayan ≠ gönderen", pre["sod"].GetProperty("label").GetString());
        Assert.False(candidate.GetProperty("canRelease").GetBoolean());

        var asPublisher = Data(await Controller(approved, Reviewer, Read, Publish).StudioReview(PathId, default));
        var ok = asPublisher.GetProperty("release").GetProperty("candidate");
        Assert.True(ok.GetProperty("canRelease").GetBoolean());
        Assert.False(ok.GetProperty("canRender").GetBoolean());                  // already rendered
        var artifact = ok.GetProperty("artifact");
        Assert.Equal("KP-A-v1.0-R2.pdf", artifact.GetProperty("fileName").GetString());
        Assert.EndsWith("&download=true", artifact.GetProperty("downloadUrl").GetString());
        Assert.Equal("1.0", ok.GetProperty("replacesVersion").GetString());      // "replaces v1.0 on the field"
        Assert.Equal("release", asPublisher.GetProperty("next").GetProperty("key").GetString());
        Assert.True(asPublisher.GetProperty("tabs").GetProperty("output").GetBoolean());
    }

    [Fact]
    public async Task The_review_model_never_offers_a_raw_code_as_a_label()
    {
        using var _ = new UiCulture("tr");
        var page = JsonSerializer.Serialize(Data(await Controller(new StubGateway(), Reviewer, Read, Publish).StudioReview(PathId, default)))
                   + JsonSerializer.Serialize(Data(await Controller(new StubGateway(), Reviewer, Read).ReviewerView(PathId, Rev2, default)));
        Assert.DoesNotContain("\"statusLabel\":\"in-review\"", page);
        Assert.DoesNotContain("\"statusLabel\":\"rejected\"", page);
        Assert.DoesNotContain("\"stepName\":\"legal\"", page);
        Assert.DoesNotContain("\"stateLabel\":\"pending\"", page);
        Assert.DoesNotContain(Author, page);                                      // person ids never shown
        Assert.DoesNotContain(Reviewer, page);
        Assert.DoesNotContain("\"countryName\":\"TR\"", page);
        Assert.DoesNotContain("\"languageName\":\"tr\"", page);
    }

    // ============================================================ reviewer view model

    [Fact]
    public async Task The_reviewer_view_shows_the_frozen_composition_claim_text_and_note_counts()
    {
        using var _ = new UiCulture("tr");
        var model = Data(await Controller(new StubGateway(), Reviewer, Read).ReviewerView(PathId, Rev2, default));
        var branches = model.GetProperty("branches").EnumerateArray().ToList();
        Assert.Equal(["Fatigue", "Main"], branches.Select(b => b.GetProperty("name").GetString()!).ToArray());
        var need = branches[1].GetProperty("slots")[0];
        Assert.Equal(("Need", $"BR1:{TypeNeed}"), (need.GetProperty("name").GetString(), need.GetProperty("stepRef").GetString()));
        Assert.Equal(1, need.GetProperty("noteCount").GetInt32());                // the open note on the step
        var items = need.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["content", "claim"], items.Select(i => i.GetProperty("kind").GetString()!).ToArray());
        Assert.Equal(("Almiba sunumu", "Sürüm 3.1"), (items[0].GetProperty("title").GetString(), items[0].GetProperty("versionLabel").GetString()));
        Assert.Equal(("Levokarnitin enerji metabolizmasını destekler.", "Diyaliz hastalarında", VersionOk.ToString()),
            (items[1].GetProperty("text").GetString(), items[1].GetProperty("qualifier").GetString(), items[1].GetProperty("countryVersionId").GetString()));
        Assert.Equal(("Türkiye", "Türkçe"), (model.GetProperty("context").GetProperty("countryName").GetString(),
            model.GetProperty("context").GetProperty("languageName").GetString()));

        var note = Assert.Single(model.GetProperty("notes").EnumerateArray());
        Assert.Equal(("Dr. Mert Demir", "Rev 1’dan taşındı", "Main › Need"),
            (note.GetProperty("author").GetString(), note.GetProperty("carriedLabel").GetString(), note.GetProperty("target").GetString()));
        Assert.False(note.GetProperty("canResolve").GetBoolean());                // neither author nor manager
    }

    // ============================================================ compliance (workspace) + rules

    [Fact]
    public async Task Workspace_compliance_rows_are_labelled_with_place_message_fix_and_target()
    {
        using var _ = new UiCulture("tr");
        var model = Data(await Controller(new StubGateway { WithIssues = true }, Author, Read, Manage).StudioWorkspace(PathId, default));
        var compliance = model.GetProperty("compliance");
        var rows = compliance.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(rows.Count, compliance.GetProperty("blockers").GetInt32());
        Assert.Equal(["chain", "placement", "published", "language", "claim"], rows.Select(r => r.GetProperty("rule").GetString()!).ToArray());
        var unpublished = rows[2];
        Assert.Equal(("Engelleyici", "İçerik yayında", "Main › Need › Klinik özet taslağı"),
            (unpublished.GetProperty("severityLabel").GetString(), unpublished.GetProperty("ruleLabel").GetString(), unpublished.GetProperty("where").GetString()));
        Assert.Equal("“Klinik özet taslağı” Bilgi Bankası’nda Taslak durumda.", unpublished.GetProperty("message").GetString());
        Assert.Equal((StepB.ToString(), "content", "BR1"), (unpublished.GetProperty("itemId").GetString(),
            unpublished.GetProperty("itemKind").GetString(), unpublished.GetProperty("branchCode").GetString()));
        Assert.Equal("“Almiba deck (EN)” İngilizce; yolun dili Türkçe.", rows[3].GetProperty("message").GetString());
        Assert.Equal("CLM-DRAFT yolun ülkesinde (Türkiye) onaylı değil.", rows[4].GetProperty("message").GetString());

        var page = JsonSerializer.Serialize(compliance);
        foreach (var code in new[] { "component_not_published", "component_language_mismatch", "claim_not_approved", "not_approved", "\"under\"" })
            Assert.DoesNotContain(code, page);                                    // no raw code (mockup v2 note)
    }

    [Fact]
    public void The_timeline_marks_rejections_and_leaves_a_closed_round_not_reached()
    {
        using var _ = new UiCulture("tr");
        var plan = new[] { new R.PlanStep("medical", "Medikal", []), new R.PlanStep("legal", "Hukuk", []), new R.PlanStep("regulatory", "Ruhsat", []) };
        var history = new[]
        {
            new R.HistoryEntry("start", "medical", "Medikal", null, Author, null, DateTimeOffset.UnixEpoch),
            new R.HistoryEntry("approve", "medical", "Medikal", null, "u1", "Dr. A", DateTimeOffset.UnixEpoch),
            new R.HistoryEntry("start", "legal", "Hukuk", null, null, null, DateTimeOffset.UnixEpoch),
            new R.HistoryEntry("reject", "legal", "Hukuk", "Yan etki satırı eksik.", "u2", "Av. B", DateTimeOffset.UnixEpoch)
        };
        var rows = R.Timeline(plan, history, roundOpen: false, Labels(), (id, display) => display ?? "?");
        Assert.Equal(["approved", "rejected", "not-reached"], rows.Select(r => r.State).ToArray());
        Assert.Equal(("Av. B", "Yan etki satırı eksik.", "Reddetti"), (rows[1].Actor, rows[1].Comment, rows[1].StateLabel));

        var noPlan = R.Timeline(null, history, roundOpen: false, Labels(), (id, display) => display ?? "?");
        Assert.Equal(["Medikal", "Hukuk"], noPlan.Select(r => r.StepName).ToArray());
    }

    [Fact]
    public void The_difference_uses_the_crm_change_vocabulary_by_step_and_claim_code()
    {
        using var _ = new UiCulture("tr");
        var from = new[] { new R.DiffItem("S-1", "Giriş", "Main › Need", 0, "1.0", "KC-1"), new R.DiffItem("S-2", "Kanıt", "Main › Need", 1, "1.0", "KC-2") };
        var to = new[] { new R.DiffItem("S-1", "Giriş", "Main › Need", 0, "1.1", "KC-1"), new R.DiffItem("S-3", "Yorgunluk", "Fatigue › Fatigue step", 0, "1.0", "KC-3") };
        var claimsFrom = new[] { new R.DiffItem("CLM-1", "CLM-1", "Main › Need", 2, "0.9", null) };
        var claimsTo = new[] { new R.DiffItem("CLM-1", "CLM-1", "Main › Need", 2, "1.0", null), new R.DiffItem("CLM-2", "CLM-2", "Main › Need", 3, "1.0", null) };
        var rows = R.Diff(from, to, claimsFrom, claimsTo, Labels());
        Assert.Equal(["content-version-changed", "step-added", "step-removed", "claim-version-changed", "claim-added"], rows.Select(r => r.Kind).ToArray());
        Assert.Equal(("İçerik sürümü değişti", "1.0", "1.1"), (rows[0].KindLabel, rows[0].From, rows[0].To));
    }

    [Fact]
    public void Release_preconditions_follow_the_compliance_categories_and_the_person_rule()
    {
        using var _ = new UiCulture("tr");
        var claimRow = new R.ComplianceRow(R.Rules.Claim, R.Blocker, "x", "x", "x", "x", "x", null, null, null, null);
        var rows = R.ReleasePreconditions(true, true, false, [claimRow], 2, Labels());
        Assert.Equal(["approved", "artifact", "published", "language", "claims", "conformance", "sod"], rows.Select(r => r.Key).ToArray());
        Assert.Equal(["claims"], rows.Where(r => !r.Ok).Select(r => r.Key).ToArray());
        var unapproved = R.ReleasePreconditions(false, false, true, [], 3, Labels());
        Assert.Equal("Rev 3 henüz onaylanmadı.", unapproved.Single(r => r.Key == "approved").Reason);
        Assert.False(unapproved.Single(r => r.Key == "sod").Ok);
    }

    // ============================================================ usage + wizard

    [Fact]
    public async Task Usage_lists_stages_and_templates_with_labels_and_links()
    {
        using var _ = new UiCulture("tr");
        var data = Data(await Controller(new StubGateway(), Reviewer, Read).StudioUsage(PathId, default));
        var journey = Assert.Single(data.GetProperty("journeys").EnumerateArray());
        Assert.Equal(("HD yolculuğu", "İlk ziyaret", "Yayında", "v1.0 sürümüne sabit"), (journey.GetProperty("name").GetString(),
            journey.GetProperty("stageName").GetString(), journey.GetProperty("statusLabel").GetString(), journey.GetProperty("pinLabel").GetString()));
        Assert.StartsWith("/CRM/ContentEngagementJourneys/Details/", journey.GetProperty("url").GetString());
        var template = Assert.Single(data.GetProperty("strategyTemplates").EnumerateArray());
        Assert.Equal(("Almiba play", "Aktif"), (template.GetProperty("name").GetString(), template.GetProperty("statusLabel").GetString()));
    }

    [Fact]
    public async Task Claim_suggestions_come_only_from_the_step_contents_claim_references_with_their_step()
    {
        var data = Data(await Controller(new StubGateway(), Author, Read, Manage).ClaimSuggestions(PathId, default));
        var suggestion = Assert.Single(data.GetProperty("suggestions").EnumerateArray());
        Assert.Equal((ClaimDraft.ToString(), "CLM-DRAFT", "BR1", TypeNeed.ToString()),
            (suggestion.GetProperty("claimId").GetString(), suggestion.GetProperty("code").GetString(),
             suggestion.GetProperty("branchCode").GetString(), suggestion.GetProperty("chainStepId").GetString()));
        Assert.False(suggestion.GetProperty("usable").GetBoolean());
    }

    [Fact]
    public void The_wizard_never_moves_on_before_every_suggestion_is_decided_and_binds_only_accepted_ones()
    {
        var script = Script("legacy-wizard.js");
        Assert.Contains("const allDecided = () => state.suggestions.every(isDecided);", script);
        Assert.Contains("next.disabled = !allDecided();", script);
        Assert.Contains("if (!allDecided()) { state.busy = false; refreshNext(); return; }", script);
        Assert.Contains("state.suggestions.filter(x => !x.onPath && state.decisions[x.claimId] === ACCEPT)", script);
        Assert.DoesNotContain("openBindModal", Script("studio-common.js"));        // the KP-UI-1 modal is gone
        Assert.DoesNotContain("openBindModal", Script("index.js"));
        Assert.DoesNotContain("openBindModal", Script("workspace.js"));
    }

    // ============================================================ error codes → user text

    [Fact]
    public void Every_review_error_code_is_a_crm_code()
    {
        // Anchored to production: every listed code is a const string value somewhere in the CRM service.
        var crm = string.Join("\n", Directory.EnumerateFiles(Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));
        var values = Regex.Matches(crm, "const string \\w+\\s*=\\s*\"(?<c>[a-z_]+)\"").Select(m => m.Groups["c"].Value).ToHashSet();
        Assert.Equal(KnowledgePathsController.ReviewErrorCodes.Count, KnowledgePathsController.ReviewErrorCodes.Distinct().Count());
        Assert.All(KnowledgePathsController.ReviewErrorCodes, code => Assert.True(values.Contains(code), $"{code} is not a CRM code"));
        foreach (var code in new[] { "review_round_open", "sod_submitter_cannot_decide", "sod_submitter_cannot_release", "path_in_use",
                     "previous_path_in_use", "artifact_missing", "revision_not_approved", "claim_not_approved", "approval_template_missing" })
            Assert.Contains(code, KnowledgePathsController.ReviewErrorCodes);
    }

    [Fact]
    public void Every_review_error_code_has_a_user_text_in_seven_languages_and_the_client_maps_it()
    {
        var script = Script("studio-common.js");
        foreach (var code in KnowledgePathsController.ReviewErrorCodes)
        {
            Assert.Matches($@"\b{Regex.Escape(code)}: \(\) => t\('Err_{Regex.Escape(code)}'\)", script);
            foreach (var language in Languages)
            {
                var values = Values(language);
                Assert.True(values.TryGetValue("Err_" + code, out var text) && !string.IsNullOrWhiteSpace(text), $"{language}: Err_{code}");
                Assert.DoesNotContain(code, text);
            }
        }

        // path_in_use carries the stage names after [code, message] — the client keeps them as details.
        Assert.Contains("details: errors.slice(errors.indexOf(known) + 2)", script);
    }

    // ============================================================ L10n + style

    [Theory]
    [InlineData("legacy-wizard.js")]
    [InlineData("workspace-review.js")]
    [InlineData("review.js")]
    public void Every_key_a_review_script_reads_exists_in_the_family(string script)
    {
        var used = Regex.Matches(Script(script), @"\bt\('([A-Za-z0-9_\-]+)'").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(used);
        var keys = Values("en");
        Assert.All(used, key => Assert.True(keys.ContainsKey(key), $"{script} reads '{key}' which KnowledgePathStudio.en.resx lacks"));
    }

    [Fact]
    public void Dynamic_review_label_families_cover_every_value()
    {
        var keys = Values("en");
        Assert.All(["approved", "rejected", "pending", "queued", "cancelled", "not-reached", "submitted"], s => Assert.True(keys.ContainsKey("MlrState_" + s), s));
        Assert.All(["in-review", "approved", "rejected", "withdrawn", "timed-out", "other"], s => Assert.True(keys.ContainsKey("RevStatus_" + s), s));
        Assert.All(["released", "withdrawn", "other"], s => Assert.True(keys.ContainsKey("Release_" + s), s));
        Assert.All(["chain", "published", "language", "claim", "placement"], s => Assert.True(keys.ContainsKey("Rule_" + s), s));
        Assert.All(["approved", "artifact", "published", "language", "claims", "conformance", "sod"], s => Assert.True(keys.ContainsKey("Pre_" + s), s));
        Assert.All(["blocker", "warning"], s => Assert.True(keys.ContainsKey("Sev_" + s), s));
        var crm = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src", "Diten.CrmService.Domain", "Entities", "KnowledgePathRevision.cs"));
        var kinds = Regex.Matches(Regex.Match(crm, @"class KnowledgePathChangeKinds\s*\{(?<b>.*?)\n\}", RegexOptions.Singleline).Groups["b"].Value,
            "const string \\w+ = \"(?<c>[^\"]+)\"").Select(m => m.Groups["c"].Value).ToList();
        Assert.Equal(9, kinds.Count);
        Assert.All(kinds, kind => Assert.True(keys.ContainsKey("Change_" + kind), $"Change_{kind} missing"));
    }

    [Fact]
    public void Review_styles_use_theme_variables_and_logical_properties()
    {
        foreach (var view in new[] { "Workspace.cshtml", "Review.cshtml" })
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "CRM", "KnowledgePaths", view));
            var style = Regex.Match(text, "<style>(?<s>.*?)</style>", RegexOptions.Singleline).Groups["s"].Value;
            Assert.DoesNotMatch("#[0-9A-Fa-f]{3,6}\\b", style);
            Assert.DoesNotMatch(@"(margin|padding|border|inset)-(left|right)\b", style);
            Assert.DoesNotMatch(@"\b(left|right)\s*:", style);
        }

        // Tabs are keyboard-reachable and mirrored under RTL; the reviewer blocks are focusable buttons.
        var review = Script("workspace-review.js");
        Assert.Contains("['ArrowRight', 'ArrowLeft', 'Home', 'End']", review);
        Assert.Contains("document.documentElement.dir === 'rtl'", review);
        Assert.Contains("tabindex=\"0\" role=\"button\" data-block-ref", Script("review.js"));
    }

    // ============================================================ helpers

    private const string Reader = Reviewer;

    private static Func<string, string> Labels()
    {
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        var localizer = new StringLocalizer<KnowledgePathStudio>(factory);
        return key => localizer[key].Value;
    }

    private static JsonElement Data(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return JsonDocument.Parse(JsonSerializer.Serialize(ok.Value)).RootElement.GetProperty("data").Clone();
    }

    private static KnowledgePathsController Controller(StubGateway gateway, string? sub, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        var controller = new KnowledgePathsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            new StringLocalizer<KnowledgePathStudio>(factory), NullLogger<KnowledgePathsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        if (sub is not null) claims.Add(new Claim("sub", sub));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private static string Script(string name) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "CRM", "KnowledgePaths", name));

    private static Dictionary<string, string> Values(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "KnowledgePaths", $"KnowledgePathStudio.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class UiCulture : IDisposable
    {
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

        public UiCulture(string name)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(name);
            CultureInfo.CurrentCulture = new CultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _ui;
            CultureInfo.CurrentCulture = _culture;
        }
    }

    // ---------------- gateway stub (the KP-1 / KP-2 / KP-3 / MOD-0023 shapes) ----------------

    private sealed class StubGateway : HttpMessageHandler
    {
        /// <summary>false: Rev 2 in review (Legal waiting). true: Rev 2 approved + rendered, path approved, v1.0 live.</summary>
        public bool Approved { get; init; }
        public bool WithIssues { get; init; }
        public string CreatedBy { get; init; } = Author;
        public (int Status, byte[] Body, string? FileName)? Artifact { get; init; }
        public List<string> Requests { get; } = [];
        public List<(string Method, string Url, string Body)> Writes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(url);
            if (request.Method != HttpMethod.Get)
            {
                Writes.Add((request.Method.Method, url, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
                return Reply(200, "{\"data\":true}");
            }

            var p = $"/api/crm/knowledge/paths/{PathId}";
            if (path == $"{p}/revisions/{Rev2}/artifact" && Artifact is { } art)
            {
                var response = new HttpResponseMessage((HttpStatusCode)art.Status) { Content = new ByteArrayContent(art.Body) };
                if (art.Body.Length > 0)
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue(art.Status == 200 ? "application/pdf" : "application/json");
                if (art.FileName is not null)
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = art.FileName };
                return response;
            }

            if (path == p) return Reply(200, Data(PathDetail()));
            if (path == $"{p}/revisions") return Reply(200, Data(new[] { Summary(Rev2, 2), Summary(Rev1, 1) }));
            if (path == $"{p}/revisions/{Rev2}") return Reply(200, Data(Revision2()));
            if (path == $"{p}/revisions/{Rev1}") return Reply(200, Data(Revision1()));
            if (path == $"{p}/review-history") return Reply(200, Data(History()));
            if (path == $"{p}/usage")
                return Reply(200, Data(new
                {
                    journeys = new[] { new { journeyId = Guid.NewGuid(), code = "CEJ-HD", name = "HD yolculuğu", status = "published", stageCode = "ST-1", stageName = "İlk ziyaret", pinPolicy = "pinned", pinnedVersion = "1.0" } },
                    strategyTemplates = new[] { new { templateId = Guid.NewGuid(), code = "ST-ALM", name = "Almiba play", status = "active" } }
                }));
            if (path == "/api/crm/knowledge/paths")
                return Reply(200, Data(new { items = Approved ? new object[] { new { pathId = Guid.NewGuid(), pathCode = "KP-A", pathVersion = "1.0", pathStatus = "published" } } : [] }));
            if (path == $"/api/crm/knowledge/concept-chain-templates/{ChainId}") return Reply(200, Data(Template()));
            if (path == "/api/crm/knowledge/concept-types")
                return Reply(200, Data(new { items = new[] { new { conceptTypeId = TypeNeed, conceptTypeName = "Need" }, new { conceptTypeId = TypeFatigue, conceptTypeName = "Fatigue step" } } }));
            if (path.StartsWith("/api/crm/knowledge/contents/"))
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                var content = Contents().FirstOrDefault(c => JsonSerializer.Serialize(c).Contains(id));
                return content is null ? Reply(404, "{}") : Reply(200, Data(content));
            }
            if (path == $"/api/crm/content-composition/claims/country-versions/{VersionOk}")
                return Reply(200, Data(new
                {
                    texts = new[] { new { languageCode = "tr", text = "Levokarnitin enerji metabolizmasını destekler." } },
                    qualifiers = new[] { new { languageCode = "tr", text = "Diyaliz hastalarında" } }
                }));
            if (path == $"/api/crm/content-composition/claims/{ClaimOk}") return Reply(200, Data(new { claimId = ClaimOk, claimName = "Metabolizma" }));
            if (path == "/api/crm/content-composition/claims/coverage")
                return Reply(200, Data(new
                {
                    rows = new object[]
                    {
                        new { claimId = ClaimOk, claimCode = "CLM-OK", claimName = "Metabolizma", kind = "core", coreVersion = "2.0", coreStatus = "approved",
                              cells = new object[] { new { countryCode = "TR", state = "approved", versionId = (Guid?)VersionOk, version = "1.0" } } },
                        new { claimId = ClaimDraft, claimCode = "CLM-DRAFT", claimName = "Taslak", kind = "core", coreVersion = "1.0", coreStatus = "approved",
                              cells = new object[] { new { countryCode = "TR", state = "draft", versionId = (Guid?)Guid.NewGuid(), version = "0.1" } } }
                    }
                }));
            // MOD-0023 (Platform) — the KP-MLR-TR plan.
            if (path == "/api/v1/workflow/definitions") return Reply(200, Data(new[] { new { id = "def-1", templateCode = "KP-MLR-TR" } }));
            if (path == "/api/v1/workflow/definitions/def-1") return Reply(200, Data(new { activePublishedVersionId = "ver-1" }));
            if (path == "/api/v1/workflow/definitions/def-1/versions/ver-1")
                return Reply(200, Data(new
                {
                    definitionJson = JsonSerializer.Serialize(new
                    {
                        stages = new[]
                        {
                            new
                            {
                                code = "mlr", name = "MLR",
                                steps = new[]
                                {
                                    new { code = "medical", name = "Medikal inceleme", assignment = new { candidatePrincipalIds = new[] { "position:p-med" } } },
                                    new { code = "legal", name = "Hukuk inceleme", assignment = new { candidatePrincipalIds = new[] { "position:p-legal" } } },
                                    new { code = "regulatory", name = "Ruhsat inceleme", assignment = new { candidatePrincipalIds = new[] { "position:p-reg" } } }
                                }
                            }
                        }
                    })
                }));
            if (path == "/api/v1/workflow/lookups/positions")
                return Reply(200, Data(new[] { new { id = "p-med", name = "Medikal Direktör" }, new { id = "p-legal", name = "Hukuk Müdürü" }, new { id = "p-reg", name = "Ruhsat Uzmanı" } }));
            return Reply(200, "{\"data\":{\"items\":[]}}");
        }

        private static string Data(object data) => JsonSerializer.Serialize(new { data });

        private static HttpResponseMessage Reply(int status, string body) =>
            new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static object Arr(Guid chainStepId, string branch, int position) => new { chainStepId, branchCode = branch, position };

        private object PathDetail() => new
        {
            pathId = PathId, pathCode = "KP-A", pathName = "Almiba detay", pathVersion = "1.1",
            pathStatus = Approved ? "approved" : "review",
            subjectId = SubjectId, countryCode = "TR", languageCode = "tr", isLegacyUnapproved = false, isStepSetFrozen = false, isArchived = false,
            chainTemplate = new { id = ChainId, code = "TPL-ALM", name = "Almiba HD chain", version = "1.2" },
            derivedContext = new { productId = Guid.NewGuid(), productName = "ALMIBA 1 g", audiences = Array.Empty<object>() },
            steps = WithIssues
                ? new object[]
                {
                    new { stepId = StepA, stepOrder = 10, stepCode = "S-1", stepTitle = "Almiba sunumu", contentId = ContentTr, isArchived = false, arrangement = Arr(TypeNeed, "BR1", 0) },
                    new { stepId = StepB, stepOrder = 20, stepCode = "S-2", stepTitle = "Klinik", contentId = ContentDraft, isArchived = false, arrangement = Arr(TypeNeed, "BR1", 1) },
                    new { stepId = Guid.NewGuid(), stepOrder = 30, stepCode = "S-4", stepTitle = "EN", contentId = ContentEn, isArchived = false, arrangement = Arr(TypeNeed, "BR1", 2) },
                    new { stepId = StepLoose, stepOrder = 5, stepCode = "S-3", stepTitle = "Eski bölüm", contentId = ContentTr, isArchived = false, arrangement = (object?)null }
                }
                : new object[]
                {
                    new { stepId = StepA, stepOrder = 10, stepCode = "S-1", stepTitle = "Almiba sunumu", contentId = ContentTr, isArchived = false, arrangement = Arr(TypeNeed, "BR1", 0) }
                },
            claims = WithIssues
                ? new object[]
                {
                    new { claimId = ClaimOk, claimCode = "CLM-OK", usable = true, reason = (string?)null, arrangement = Arr(TypeNeed, "BR1", 3) },
                    new { claimId = ClaimDraft, claimCode = "CLM-DRAFT", usable = false, reason = "not_approved", arrangement = Arr(TypeNeed, "BR1", 4) }
                }
                : new object[] { new { claimId = ClaimOk, claimCode = "CLM-OK", usable = true, reason = (string?)null, arrangement = Arr(TypeNeed, "BR1", 1) } },
            chainConformance = WithIssues
                ? new object[]
                {
                    new { branchCode = "BR2", chainStepId = TypeFatigue, count = 0, min = 1, max = (int?)null, status = "under" },
                    new { branchCode = "BR1", chainStepId = TypeNeed, count = 5, min = 1, max = (int?)9, status = "ok" }
                }
                : new object[]
                {
                    new { branchCode = "BR2", chainStepId = TypeFatigue, count = 0, min = 0, max = (int?)null, status = "ok" },
                    new { branchCode = "BR1", chainStepId = TypeNeed, count = 2, min = 1, max = (int?)2, status = "ok" }
                }
        };

        private object Template() => new
        {
            conceptChainTemplateId = ChainId, subjectId = SubjectId, chainName = "Almiba HD chain", chainVersion = "1.2", status = "published",
            branches = new object[]
            {
                new { branchCode = "BR1", branchName = "Main", sortOrder = 1, steps = new object[] { new { conceptTypeId = TypeNeed, minSelection = 1, maxSelection = (int?)2 } } },
                new { branchCode = "BR2", branchName = "Fatigue", sortOrder = 0, steps = new object[] { new { conceptTypeId = TypeFatigue, minSelection = 0, maxSelection = (int?)null } } }
            }
        };

        private static object[] Contents() =>
        [
            new { contentId = ContentTr, contentCode = "KC-1", contentTitle = "Almiba sunumu", contentStatus = "published", languageCode = "tr", contentVersion = "3.1",
                  claimRefs = new[] { new { claimId = ClaimDraft, claimCode = "CLM-DRAFT" } } },
            new { contentId = ContentDraft, contentCode = "KC-3", contentTitle = "Klinik özet taslağı", contentStatus = "draft", languageCode = "tr", contentVersion = "0.1",
                  claimRefs = Array.Empty<object>() },
            new { contentId = ContentEn, contentCode = "KC-2", contentTitle = "Almiba deck (EN)", contentStatus = "published", languageCode = "en", contentVersion = "1.0",
                  claimRefs = Array.Empty<object>() }
        ];

        private object Round(Guid instance, bool open, string? outcome) => new
        {
            workflowInstanceId = instance, templateCode = "KP-MLR-TR", submittedAt = "2026-09-27T13:10:00+00:00", submittedBy = Author,
            outcome, closedAt = (string?)null, completedBy = (string?)null, reasonCode = (string?)null, isOpen = open
        };

        private object Summary(Guid id, int number) => new { revisionId = id, pathId = PathId, revisionNumber = number };

        private object Revision2() => new
        {
            revisionId = Rev2, pathId = PathId, pathCode = "KP-A", pathVersion = "1.1", revisionNumber = 2,
            status = Approved ? "approved" : "in-review", submittedBy = CreatedBy, createdAt = "2026-09-27T13:10:00+00:00",
            round = Round(Instance2, !Approved, Approved ? "approved" : null),
            snapshot = new
            {
                conceptChainTemplateId = ChainId, chainVersion = "1.2", pathName = "Almiba detay", countryCode = "TR", languageCode = "tr", productName = "ALMIBA 1 g",
                steps = new object[] { new { stepId = StepA, stepCode = "S-1", stepTitle = "Almiba sunumu", stepOrder = 10, contentId = ContentTr, contentCode = "KC-1", contentVersion = "3.1", isRequired = true, arrangement = Arr(TypeNeed, "BR1", 0) } },
                claims = new object[] { new { claimId = ClaimOk, claimCode = "CLM-OK", claimVersion = "2.0", countryVersionId = VersionOk, countryVersion = "1.0", countryVersionStatus = "approved", arrangement = Arr(TypeNeed, "BR1", 1) } },
                conformance = Array.Empty<object>()
            },
            notes = new object[]
            {
                new { noteId = NoteId, stepRef = $"BR1:{TypeNeed}", blockRef = (string?)null, text = "Hukuk da baksın.", author = "user-medical",
                      createdAt = "2026-09-27T14:00:00+00:00", resolvedAt = (string?)null, resolvedBy = (string?)null, carriedFromRevision = 1, isResolved = false }
            },
            changeSummary = new { comparedToRevision = 1, items = new[] { new { kind = "claim-version-changed", @ref = "CLM-OK", label = (string?)null, from = "0.9", to = "1.0" } } },
            version = 3,
            artifacts = Approved
                ? new object[] { new { kind = "pdf", contentId = Guid.NewGuid(), checksum = "sha256:9f3a00000000c21e", byteSize = 1887436, mediaType = "application/pdf", fileName = "KP-A-v1.0-R2.pdf", renderedAt = "2026-09-29T09:00:00+00:00", renderedBy = "user-publisher" } }
                : Array.Empty<object>(),
            release = (object?)null
        };

        private object Revision1() => new
        {
            revisionId = Rev1, pathId = PathId, pathCode = "KP-A", pathVersion = "1.1", revisionNumber = 1, status = "rejected",
            submittedBy = Author, createdAt = "2026-09-20T10:00:00+00:00", round = Round(Guid.NewGuid(), false, "rejected"),
            snapshot = new { conceptChainTemplateId = ChainId, languageCode = "tr", countryCode = "TR", steps = Array.Empty<object>(), claims = Array.Empty<object>() },
            notes = Array.Empty<object>(), changeSummary = new { comparedToRevision = (int?)null, items = Array.Empty<object>() }, version = 2,
            artifacts = Array.Empty<object>(), release = (object?)null
        };

        private object[] History() =>
        [
            new
            {
                revisionId = Rev2, revisionNumber = 2, workflowInstanceId = Instance2, available = true,
                entries = Approved
                    ? new object[]
                    {
                        new { action = "start", stepCode = "medical", stepName = "Medikal inceleme", actorId = Author, actorDisplay = "Ayşe Kaya", occurredAt = "2026-09-27T13:10:00+00:00" },
                        new { action = "approve", stepCode = "medical", stepName = "Medikal inceleme", comment = "Mekanizma cümlesi uygun.", actorId = "user-medical", actorDisplay = "Dr. Mert Demir", occurredAt = "2026-09-28T09:00:00+00:00" },
                        new { action = "approve", stepCode = "legal", stepName = "Hukuk inceleme", actorId = Reviewer, actorDisplay = "Av. Selin Aksoy", occurredAt = "2026-09-28T11:00:00+00:00" },
                        new { action = "approve", stepCode = "regulatory", stepName = "Ruhsat inceleme", actorId = "user-reg", actorDisplay = "Oğuz B.", occurredAt = "2026-09-28T15:00:00+00:00" }
                    }
                    : new object[]
                    {
                        new { action = "start", stepCode = "medical", stepName = "Medikal inceleme", actorId = Author, actorDisplay = "Ayşe Kaya", occurredAt = "2026-09-27T13:10:00+00:00" },
                        new { action = "approve", stepCode = "medical", stepName = "Medikal inceleme", comment = "Mekanizma cümlesi uygun.", actorId = "user-medical", actorDisplay = "Dr. Mert Demir", occurredAt = "2026-09-28T09:00:00+00:00" },
                        new { action = "start", stepCode = "legal", stepName = "Hukuk inceleme", actorId = (string?)null, actorDisplay = (string?)null, occurredAt = "2026-09-28T09:00:01+00:00" }
                    }
            }
        ];
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
