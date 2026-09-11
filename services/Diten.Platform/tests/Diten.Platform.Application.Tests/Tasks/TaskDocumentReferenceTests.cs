using System.Reflection;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// DCP-005 Step 2 — a fake <see cref="IControlledDocumentCitationPort"/> over an in-memory row set, for the tests
/// below. Mirrors what <c>FakeDocumentReferenceListRepository</c> (<c>DocumentReferenceListTests.cs</c>) did for
/// the CSV era: a controllable double, not a stub — <see cref="ResolveAsync"/> genuinely omits unmatched
/// identifiers rather than fabricating a result for them, the same "Unresolved is absence" contract the real port
/// promises (contract §2).
/// </summary>
internal sealed class FakeControlledDocumentCitationPort : IControlledDocumentCitationPort
{
    public List<DocumentCitationItem> Items { get; } = [];

    /// <summary>Set to make the next <see cref="ResolveAsync"/> throw — the fail-closed "could not check" path.</summary>
    public Exception? ThrowOnResolve { get; set; }

    /// <summary>UIDs actually asked for, across every call — so a test can prove a UID was never re-queried.</summary>
    public List<string> ResolvedIdentifiers { get; } = [];

    public Task<DocumentCitationResult> ResolveAsync(DocumentCitationQuery query, CancellationToken ct)
    {
        ResolvedIdentifiers.AddRange(query.Identifiers);
        if (ThrowOnResolve is { } error) { throw error; }

        var matched = Items
            .Where(i => query.Identifiers.Contains(i.Uid, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(new DocumentCitationResult(matched));
    }

    public Task<DocumentCitationResult> SearchAsync(string? term, int limit, CancellationToken ct)
        => Task.FromResult(new DocumentCitationResult(Items));
}

/// <summary>
/// DCP-005 Step 2 (WP-PSS-DCP005-STEP2-CITATION-REPOINT-01) — a task citing a controlled document, now resolved
/// against the live Document Master Register instead of the CSV list.
///
/// <para>The subject of every test here is still ONE rule: the frozen fields are set when the citation is made
/// and nothing in the product rewrites them afterwards. It is a rule that fails silently — a refreshed title
/// looks like a correction, not like a defect — so it is pinned from both directions: what a re-resolve WOULD
/// change, and that the code never re-resolves (proven here by a UID already cited never reaching the port a
/// second time — see <see cref="A_frozen_citation_is_never_re_resolved_from_the_register"/>).</para>
/// </summary>
public sealed class TaskDocumentReferenceFreezeTests
{
    private static DocumentCitationItem Citation(
        string uid, string code, string title, string? version = "1.0", string lifecycle = "Effective",
        bool citable = true, string? blockedReason = null)
        => new(uid, code, title, version, lifecycle, citable, blockedReason,
            DocumentCitationSource.MasterRegister, Guid.NewGuid());

    private static (TaskDocumentReferenceFreezer Freezer, FakeControlledDocumentCitationPort Port) Setup()
    {
        var port = new FakeControlledDocumentCitationPort();
        port.Items.Add(Citation("UID-0000104", "GMG-QMS-SOP-0005", "Document Control"));
        port.Items.Add(Citation("UID-0000115", "GMG-GDP-SOP-0001", "Distribution Practice",
            lifecycle: "Superseded", citable: false, blockedReason: "planned, not yet issued"));
        return (new TaskDocumentReferenceFreezer(port), port);
    }

    [Fact]
    public async Task Citing_a_document_freezes_the_five_fields_with_no_list_version()
    {
        var (freezer, _) = Setup();
        var at = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);

        var result = await freezer.ResolveNewAsync([], ["UID-0000104"], at);

        Assert.True(result.Success);
        var reference = result.References.Single();
        Assert.Equal("UID-0000104", reference.DocumentUid);
        Assert.Equal("GMG-QMS-SOP-0005", reference.DocumentCode);
        Assert.Equal("Document Control", reference.Title);
        Assert.Equal("1.0", reference.DocumentVersion);
        Assert.Equal("Effective", reference.Status);
        Assert.Equal(at, reference.ReferencedAt);
        // DCP-005 Step 2 — a register-sourced citation carries no CSV list version.
        Assert.Null(reference.ListVersionId);
    }

    [Fact]
    public async Task A_frozen_citation_is_never_re_resolved_from_the_register()
    {
        /*
         * MUTATION GUARD — THE CENTRAL ONE. Make the freezer re-resolve a UID the task already cites and this
         * goes red: it fails BOTH on the assertion below (the stale title survives) AND on the exception the
         * fake port throws the second time it is asked for a UID it should never be asked for again.
         *
         * The scenario is the reason fields are frozen at all: the register moves on, the document keeps its
         * UID and gains a new title and version, and a task closed last month must keep reading the way its
         * author read it. A "helpful" refresh here is invisible — no error, no diff, and a record that now says
         * something its author never wrote.
         */
        var (freezer, port) = Setup();
        var citedAt = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);
        var first = await freezer.ResolveNewAsync([], ["UID-0000104"], citedAt);

        // The register moves on, and the same document now reads differently.
        port.Items.Clear();
        port.Items.Add(Citation("UID-0000104", "GMG-QMS-SOP-0099", "Document Control (revised)", "2.0"));
        port.ResolvedIdentifiers.Clear();

        var later = await freezer.ResolveNewAsync(first.References, ["UID-0000104"], citedAt.AddMonths(1));

        var reference = later.References.Single();
        Assert.Equal("Document Control", reference.Title);
        Assert.Equal("GMG-QMS-SOP-0005", reference.DocumentCode);
        Assert.Equal("1.0", reference.DocumentVersion);
        Assert.Equal(citedAt, reference.ReferencedAt);
        // The port was never asked — the already-cited UID short-circuits before ResolveAsync is called.
        Assert.Empty(port.ResolvedIdentifiers);
    }

    [Fact]
    public async Task A_blocked_row_cannot_be_cited_even_though_it_is_shown()
    {
        /*
         * MUTATION GUARD: drop the Citable check and this goes red.
         *
         * The picker refuses a blocked row so the reader can see WHY. This refuses it because a screen is not a
         * boundary — an API caller never passes the picker. DCP-005 Step 2 moved the judgment from the CSV flag
         * to the register's own Citable (Effective ∨ UnderRevision); the refusal itself is unchanged from main.
         * (The WP's first reading of AC4 — "Blocked freezes with its true status" — was reverted by the Control
         * Tower on 2026-09-11: the task-TYPE activation gate, Step 3, is the separate question.)
         */
        var (freezer, _) = Setup();

        var result = await freezer.ResolveNewAsync([], ["UID-0000115"], DateTimeOffset.UtcNow);

        Assert.False(result.Success);
        Assert.Equal(TaskReasonCodes.DocumentReferenceBlocked, result.ReasonCode);
        Assert.Equal("UID-0000115", result.OffendingUid);
    }

    [Fact]
    public async Task A_uid_the_register_does_not_resolve_is_refused_by_name()
    {
        var (freezer, _) = Setup();

        var result = await freezer.ResolveNewAsync([], ["UID-9999999"], DateTimeOffset.UtcNow);

        Assert.False(result.Success);
        Assert.Equal(TaskReasonCodes.DocumentReferenceNotFound, result.ReasonCode);
        Assert.Equal("UID-9999999", result.OffendingUid);
    }

    [Fact]
    public async Task A_register_read_failure_propagates_instead_of_being_swallowed()
    {
        /*
         * Fail-closed (contract §2/§3/§5): an infrastructure failure is a thrown exception, never translated into
         * a fabricated Unresolved refusal or a fabricated success. MUTATION GUARD: wrap ResolveAsync in a
         * try/catch that turns the exception into a Failed(...) result and this goes red.
         */
        var (freezer, port) = Setup();
        port.ThrowOnResolve = new InvalidOperationException("register unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => freezer.ResolveNewAsync([], ["UID-0000104"], DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Null_leaves_the_citations_alone_and_an_empty_list_clears_them()
    {
        var (freezer, _) = Setup();
        var cited = await freezer.ResolveNewAsync([], ["UID-0000104"], DateTimeOffset.UtcNow);

        // Null: every payload written before this field existed says exactly this.
        var untouched = await freezer.ResolveNewAsync(cited.References, null, DateTimeOffset.UtcNow);
        Assert.Equal(1, (untouched.References).Count);

        // Empty: the author removed it. Removal is not a change to a frozen value.
        var cleared = await freezer.ResolveNewAsync(cited.References, [], DateTimeOffset.UtcNow);
        Assert.Empty(cleared.References);
    }
}

/// <summary>
/// DCP-005 §6.4 — what a task type SUGGESTS, and the three different ways that suggestion can be empty.
/// </summary>
public sealed class TaskTypeGoverningDocumentsTests
{
    private static (GetTaskTypeGoverningDocumentsHandler Handler, FakeTaskTypeRepository Types)
        Setup(TaskType type)
    {
        var types = new FakeTaskTypeRepository(type);

        var citations = new FakeControlledDocumentCitationPort();
        citations.Items.Add(new DocumentCitationItem(
            "UID-0000104", "GMG-QMS-SOP-0005", "Document Control", "1.0", "Effective",
            Citable: true, BlockedReason: null, DocumentCitationSource.MasterRegister, Guid.NewGuid()));
        citations.Items.Add(new DocumentCitationItem(
            "UID-0000115", "GMG-GDP-SOP-0001", "Distribution Practice", "1.0", "Superseded",
            Citable: false, BlockedReason: "planned, not yet issued",
            DocumentCitationSource.MasterRegister, Guid.NewGuid()));

        return (new GetTaskTypeGoverningDocumentsHandler(types, citations), types);
    }

    private static TaskType Type(string code, List<string> group) => new()
    {
        Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Code = code, Name = code, GroupDocuments = group,
    };

    [Fact]
    public async Task A_type_suggests_its_citable_group_documents()
    {
        var type = Type("DEV-QMS", ["UID-0000104"]);
        var (handler, _) = Setup(type);

        var response = await handler.Handle(
            new GetTaskTypeGoverningDocumentsQuery(type.Id, null, "c"), CancellationToken.None);

        Assert.Equal("GMG-QMS-SOP-0005", response.Data!.Suggestions.Single().DocumentCode);
        Assert.Equal(1, response.Data.NamedCount);
    }

    [Fact]
    public async Task A_suggestion_is_never_a_requirement()
    {
        /*
         * MUTATION GUARD: make the suggestion mandatory — return it as anything the caller must keep, or refuse a
         * citation list that omits it — and this goes red.
         *
         * The query answers with a SUGGESTION and nothing else: it neither writes to the task nor constrains what
         * the freezer will accept. Proven by asking the freezer to cite NOTHING while the type suggests something,
         * and getting an empty, successful answer.
         */
        var type = Type("DEV-QMS", ["UID-0000104"]);
        var (handler, _) = Setup(type);
        var suggestion = await handler.Handle(
            new GetTaskTypeGoverningDocumentsQuery(type.Id, null, "c"), CancellationToken.None);
        Assert.NotEmpty(suggestion.Data!.Suggestions);

        var freezer = new TaskDocumentReferenceFreezer(new FakeControlledDocumentCitationPort());
        var result = await freezer.ResolveNewAsync([], [], DateTimeOffset.UtcNow);

        Assert.True(result.Success);
        Assert.Empty(result.References);
    }

    [Fact]
    public async Task An_empty_suggestion_says_WHICH_kind_of_empty_it_is()
    {
        /*
         * MEASURED against the counterparty's seed on 2026-08-26: 15 of the 31 types have no citable governing
         * document, and they are empty for three different reasons. A bare empty box would be the same picture
         * for all three, and each of them is a different thing to tell the person choosing.
         */

        // (1) names nothing at all — GEN-ADMIN, the only one of the 31.
        var namesNothing = Type("GEN-ADMIN", []);
        var (h1, _) = Setup(namesNothing);
        var r1 = (await h1.Handle(
            new GetTaskTypeGoverningDocumentsQuery(namesNothing.Id, null, "c"), CancellationToken.None)).Data!;
        Assert.Equal(0, r1.NamedCount);
        Assert.Empty(r1.UnresolvedUids);
        Assert.Empty(r1.BlockedSuggestions);

        // (2) names documents the register does not contain — 7 of the 31 (PV-CASE, RECALL, QAG …).
        var namesMissing = Type("PV-CASE", ["UID-9999999"]);
        var (h2, _) = Setup(namesMissing);
        var r2 = (await h2.Handle(
            new GetTaskTypeGoverningDocumentsQuery(namesMissing.Id, null, "c"), CancellationToken.None)).Data!;
        Assert.Empty(r2.Suggestions);
        Assert.Equal(1, r2.NamedCount);
        Assert.Equal("UID-9999999", Assert.Single(r2.UnresolvedUids));

        // (3) names documents the register refuses to link — 7 of the 31 (BATCH-RELEASE, ARTWORK, PQR …).
        var namesBlocked = Type("BATCH-RELEASE", ["UID-0000115"]);
        var (h3, _) = Setup(namesBlocked);
        var r3 = (await h3.Handle(
            new GetTaskTypeGoverningDocumentsQuery(namesBlocked.Id, null, "c"), CancellationToken.None)).Data!;
        Assert.Empty(r3.Suggestions);
        Assert.Equal("planned, not yet issued", Assert.Single(r3.BlockedSuggestions).LinkBlockedReason);
    }

    [Fact]
    public async Task The_local_layer_applies_only_to_its_own_organisation()
    {
        // §6.4 — two layers, never a cross product.
        var type = Type("DEV-QMS", []);
        type.LocalDocuments["GMG-CH"] = ["UID-0000104"];
        var (handler, _) = Setup(type);

        var mine = await handler.Handle(
            new GetTaskTypeGoverningDocumentsQuery(type.Id, "GMG-CH", "c"), CancellationToken.None);
        Assert.Single(mine.Data!.Suggestions);

        var other = await handler.Handle(
            new GetTaskTypeGoverningDocumentsQuery(type.Id, "GMG-DE", "c"), CancellationToken.None);
        Assert.Empty(other.Data!.Suggestions);
        Assert.Equal(0, other.Data.NamedCount);
    }
}

/// <summary>
/// The freezer used to be an OPTIONAL constructor argument on both write handlers, so a missing DI
/// registration compiled, passed every unit test, and dropped every citation at runtime with no error
/// anywhere.
///
/// <para>MEASURED LIVE on 2026-08-26: the form showed two documents, the task saved, and the stored record
/// carried none. This is the same failure mode as BL-024 — a seam that defaults to "do nothing" is invisible
/// in review.</para>
///
/// <para>⚠ HOW THIS USED TO BE PINNED, AND WHY THAT WAS WRONG. There was a test here that read
/// <c>DependencyInjection.cs</c> off disk and searched it for the substring
/// <c>"AddScoped&lt;…TaskDocumentReferenceFreezer&gt;()"</c>. Two things were wrong with it. It proved
/// nothing about the container — a registration can be present as text and still be unreachable, and a
/// working registration written any other way would have failed it. And it reached its own source tree by
/// climbing five directories out of the build output, which is right in one checkout shape and wrong in a
/// git worktree; measured 2026-08-27, that is what stopped the GSKU team running the full suite.</para>
///
/// <para>⚠ THE REAL FIX IS NOT A TEST (DCP-005 §6.1). The argument is now REQUIRED, and
/// <c>Program.cs</c> builds the container with <c>ValidateOnBuild</c>. Delete the registration and the
/// application does not start: "Some services are not able to be constructed … Unable to resolve service
/// for type TaskDocumentReferenceFreezer". Verified by mutation on 2026-08-27 — the host never reached
/// "Now listening on". A discipline problem became an architectural one, and no string search is
/// involved.</para>
///
/// <para>What remains here is the cheap half nothing else covers: that the argument has not quietly been
/// given a default again. Reflection, not file paths.</para>
/// </summary>
public sealed class TaskDocumentFreezerRegistrationTests
{
    [Theory]
    [InlineData(typeof(CreateTaskItemHandler))]
    [InlineData(typeof(UpdateTaskItemHandler))]
    public void The_freezer_is_a_required_constructor_argument(Type handler)
    {
        /*
         * MUTATION GUARD: write `= null` back onto the freezer parameter and this goes red — and it has to,
         * because the startup check above is only as strong as the argument being required. An optional
         * argument resolves to null instead of failing, and the container never notices.
         */
        var parameter = handler
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(p => p.ParameterType == typeof(TaskDocumentReferenceFreezer));

        Assert.False(parameter.IsOptional,
            $"{handler.Name} takes the freezer as an optional argument again. Optional means a missing DI "
            + "registration resolves to null and silently discards every citation the author entered — which "
            + "is the defect measured live on 2026-08-26.");

        Assert.False(parameter.HasDefaultValue);

        // Nullable annotation removed too: `TaskDocumentReferenceFreezer?` would compile and let a caller
        // pass null explicitly, which is the same hole reached by a different door.
        var nullability = new NullabilityInfoContext().Create(parameter);
        Assert.Equal(NullabilityState.NotNull, nullability.WriteState);
    }
}
