using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;
using Diten.CrmService.Application.Features.Knowledge.Concept.Contract;
using Diten.CrmService.Application.Features.Knowledge.Concept.Graph;
using Diten.CrmService.Application.Features.Knowledge.Concept.Link;
using Diten.CrmService.Application.Features.Knowledge.Concept.Node;
using Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;
using Diten.CrmService.Application.Features.Knowledge.Concept.Type;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Application.Features.Knowledge.Content.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0162 FU03 — concept graph runtime tests. Uses in-memory fakes; handler mutations are tracked in place (Update is
/// a no-op that returns the same reference the handler already mutated). Covers the happy paths, V01–V22, cycle
/// detection (2-hop / 3-hop), template rules, tenant isolation, the FU03 V17 content dirty-check and the fixed-depth
/// by-content graph view.
/// </summary>
public sealed class ConceptGraphRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeSubjectRepo Subjects { get; } = new();
        public FakeContentRepo Contents { get; } = new();
        public FakeTypeRepo Types { get; } = new();
        public FakeNodeRepo Nodes { get; } = new();
        public FakeRelationshipRepo Relationships { get; } = new();
        public FakeTemplateRepo Templates { get; } = new();
        public FakeLinkRepo Links { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateConceptTypeHandler CreateType() => new(Tenant(TenantId), new NullActorContext(), Types, Subjects);
        public UpdateConceptTypeHandler UpdateType() => new(Tenant(TenantId), new NullActorContext(), Types);
        public ArchiveConceptTypeHandler ArchiveType() => new(Tenant(TenantId), new NullActorContext(), Types);
        public ListConceptTypesHandler ListTypes(Guid? t = null) => new(Tenant(t ?? TenantId), Types);

        public CreateConceptNodeHandler CreateNode() => new(Tenant(TenantId), new NullActorContext(), Nodes, Types);
        public UpdateConceptNodeHandler UpdateNode() => new(Tenant(TenantId), new NullActorContext(), Nodes);
        public ArchiveConceptNodeHandler ArchiveNode() => new(Tenant(TenantId), new NullActorContext(), Nodes);

        public CreateConceptRelationshipHandler CreateRel()
            => new(Tenant(TenantId), new NullActorContext(), Relationships, Nodes, Templates);
        public UpdateConceptRelationshipHandler UpdateRel()
            => new(Tenant(TenantId), new NullActorContext(), Relationships, Nodes, Templates);
        public ListConceptRelationshipsHandler ListRels() => new(Tenant(TenantId), Relationships);

        public CreateConceptChainTemplateHandler CreateTemplate()
            => new(Tenant(TenantId), new NullActorContext(), Templates, Types);
        public UpdateConceptChainTemplateHandler UpdateTemplate()
            => new(Tenant(TenantId), new NullActorContext(), Templates, Types);

        public CreateKnowledgeContentConceptLinkHandler CreateLink()
            => new(Tenant(TenantId), new NullActorContext(), Links, Contents, Nodes, Relationships);
        public ArchiveKnowledgeContentConceptLinkHandler ArchiveLink()
            => new(Tenant(TenantId), new NullActorContext(), Links);

        public CreateKnowledgeContentHandler CreateContent()
            => new(Tenant(TenantId), new NullActorContext(), Contents, Subjects, new FakeTopicNoop(),
                new FakeProfileNoop(), Nodes);
        public UpdateKnowledgeContentHandler UpdateContent()
            => new(Tenant(TenantId), new NullActorContext(), Contents, Subjects, new FakeTopicNoop(),
                new FakeProfileNoop(), Nodes);

        public GetConceptGraphByContentHandler ByContent()
            => new(Tenant(TenantId), Links, Nodes, Relationships, Templates);

        public Guid SeedSubject(bool archived = false)
        {
            var s = new Subject
            {
                TenantId = TenantId, SubjectCode = "SUB-" + Guid.NewGuid().ToString("N")[..6],
                SubjectName = "Subject", Status = archived ? TaxonomyStatuses.Archived : TaxonomyStatuses.Active,
                EffectiveFrom = Jan1, ArchivedAt = archived ? Jan1 : null
            };
            Subjects.Items.Add(s);
            return s.Id;
        }

        public async Task<Guid> SeedType(Guid subjectId, string code = "T1")
        {
            var r = await CreateType().Handle(
                new CreateConceptTypeCommand(subjectId, code, "Type " + code, Status: ConceptStatuses.Active), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public async Task<Guid> SeedNode(Guid subjectId, Guid typeId, string code = "N1")
        {
            var r = await CreateNode().Handle(
                new CreateConceptNodeCommand(subjectId, typeId, code, "Node " + code, Jan1,
                    Status: ConceptStatuses.Active), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public Guid SeedContent(Guid subjectId, Guid? conceptNodeId = null, bool archived = false)
        {
            var c = new KnowledgeContent
            {
                TenantId = TenantId, ContentCode = "KC-" + Guid.NewGuid().ToString("N")[..6],
                ContentTitle = "Content", ContentType = KnowledgeContentTypes.Presentation,
                ContentStatus = archived ? KnowledgeContentStatuses.Archived : KnowledgeContentStatuses.Published,
                SubjectId = subjectId, ConceptNodeId = conceptNodeId, LanguageCode = "en", ContentVersion = "1.0",
                EffectiveFrom = Jan1, Url = "https://x.test", ArchivedAt = archived ? Jan1 : null
            };
            Contents.Items.Add(c);
            return c.Id;
        }
    }

    // ---------------- happy paths ----------------

    [Fact] // 1
    public async Task Create_type_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var r = await fx.CreateType().Handle(new CreateConceptTypeCommand(subjectId, "indication", "Indication"), default);
        Assert.Equal(201, r.StatusCode);
        Assert.NotEqual(Guid.Empty, r.Data);
    }

    [Fact] // 2
    public async Task Update_type_valid_returns_200()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var r = await fx.UpdateType().Handle(new UpdateConceptTypeCommand(typeId, "Renamed"), default);
        Assert.True(r.IsSuccessful);
    }

    [Fact] // 3
    public async Task Archive_type_is_idempotent()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        Assert.True((await fx.ArchiveType().Handle(new ArchiveConceptTypeCommand(typeId), default)).IsSuccessful);
        Assert.True((await fx.ArchiveType().Handle(new ArchiveConceptTypeCommand(typeId), default)).IsSuccessful);
    }

    [Fact] // 4
    public async Task Create_node_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var r = await fx.CreateNode().Handle(
            new CreateConceptNodeCommand(subjectId, typeId, "n-migraine", "Migraine", Jan1), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact] // 5
    public async Task Update_and_archive_node()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var nodeId = await fx.SeedNode(subjectId, typeId);
        Assert.True((await fx.UpdateNode().Handle(
            new UpdateConceptNodeCommand(nodeId, "New name", Jan1), default)).IsSuccessful);
        Assert.True((await fx.ArchiveNode().Handle(new ArchiveConceptNodeCommand(nodeId), default)).IsSuccessful);
    }

    [Fact] // 6
    public async Task Create_relationship_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        var r = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R1", "R1", Jan1, Status: ConceptStatuses.Active),
            default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact] // 7
    public async Task Node_external_ref_global_product_accepted()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var r = await fx.CreateNode().Handle(new CreateConceptNodeCommand(
            subjectId, typeId, "n-gp", "GP node", Jan1,
            ExternalRefType: ConceptExternalRefTypes.GlobalProduct, ExternalRefId: Guid.NewGuid().ToString()), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact] // 8
    public async Task Create_template_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var t1 = await fx.SeedType(subjectId, "T1");
        var t2 = await fx.SeedType(subjectId, "T2");
        var r = await fx.CreateTemplate().Handle(new CreateConceptChainTemplateCommand(
            subjectId, "CHAIN-1", "Chain 1", new[] { t1, t2 }, Jan1), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact] // 9
    public async Task Create_and_archive_link()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var nodeId = await fx.SeedNode(subjectId, typeId);
        var contentId = fx.SeedContent(subjectId);
        var create = await fx.CreateLink().Handle(
            new CreateKnowledgeContentConceptLinkCommand(contentId, nodeId), default);
        Assert.Equal(201, create.StatusCode);
        Assert.True((await fx.ArchiveLink().Handle(
            new ArchiveKnowledgeContentConceptLinkCommand(create.Data), default)).IsSuccessful);
    }

    // ---------------- V02–V06 duplicate & archived-parent ----------------

    [Fact] // 10  V02
    public async Task Duplicate_type_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        await fx.SeedType(subjectId, "dup");
        var second = await fx.CreateType().Handle(new CreateConceptTypeCommand(subjectId, "dup", "Dup"), default);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact] // 11  V03
    public async Task Type_under_archived_subject_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject(archived: true);
        var r = await fx.CreateType().Handle(new CreateConceptTypeCommand(subjectId, "t", "T"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 12  V04
    public async Task Node_under_archived_type_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        await fx.ArchiveType().Handle(new ArchiveConceptTypeCommand(typeId), default);
        var r = await fx.CreateNode().Handle(new CreateConceptNodeCommand(subjectId, typeId, "n", "N", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 13  V05
    public async Task Node_subject_type_mismatch_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectA = fx.SeedSubject();
        var subjectB = fx.SeedSubject();
        var typeInA = await fx.SeedType(subjectA);
        var r = await fx.CreateNode().Handle(new CreateConceptNodeCommand(subjectB, typeInA, "n", "N", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 14  V06
    public async Task Duplicate_node_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        await fx.SeedNode(subjectId, typeId, "dup");
        var second = await fx.CreateNode().Handle(
            new CreateConceptNodeCommand(subjectId, typeId, "dup", "Dup", Jan1), default);
        Assert.Equal(409, second.StatusCode);
    }

    // ---------------- relationship rules ----------------

    [Fact] // 15  V07 self-loop
    public async Task Relationship_self_loop_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var r = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n1, ConceptRelationshipTypes.LeadsTo, "R", "R", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 16  V08 cross-subject
    public async Task Relationship_cross_subject_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectA = fx.SeedSubject();
        var subjectB = fx.SeedSubject();
        var typeA = await fx.SeedType(subjectA, "TA");
        var typeB = await fx.SeedType(subjectB, "TB");
        var a = await fx.SeedNode(subjectA, typeA, "A");
        var b = await fx.SeedNode(subjectB, typeB, "B");
        var r = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectA, a, b, ConceptRelationshipTypes.LeadsTo, "R", "R", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 17  V09 archived node
    public async Task Relationship_on_archived_node_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        await fx.ArchiveNode().Handle(new ArchiveConceptNodeCommand(n2), default);
        var r = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R", "R", Jan1, Status: ConceptStatuses.Active),
            default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 18  V10 cycle 2-hop
    public async Task Relationship_cycle_two_hop_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R1", "R1", Jan1, Status: ConceptStatuses.Active),
            default);
        var back = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n2, n1, ConceptRelationshipTypes.LeadsTo, "R2", "R2", Jan1, Status: ConceptStatuses.Active),
            default);
        Assert.Equal(400, back.StatusCode);
    }

    [Fact] // 19  V10 cycle 3-hop
    public async Task Relationship_cycle_three_hop_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        var n3 = await fx.SeedNode(subjectId, typeId, "N3");
        await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R1", "R1", Jan1, Status: ConceptStatuses.Active),
            default);
        await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n2, n3, ConceptRelationshipTypes.LeadsTo, "R2", "R2", Jan1, Status: ConceptStatuses.Active),
            default);
        var close = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n3, n1, ConceptRelationshipTypes.LeadsTo, "R3", "R3", Jan1, Status: ConceptStatuses.Active),
            default);
        Assert.Equal(400, close.StatusCode);
    }

    [Fact] // 20  V11 duplicate active
    public async Task Duplicate_active_relationship_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R1", "R1", Jan1, Status: ConceptStatuses.Active),
            default);
        var dup = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R2", "R2", Jan1, Status: ConceptStatuses.Active),
            default);
        Assert.Equal(409, dup.StatusCode);
    }

    [Fact] // 21  V19 unknown relationship-type
    public async Task Unknown_relationship_type_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        var r = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, "depends-on", "R", "R", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 22  V16 non-conforming is accepted and visible
    public async Task Non_conforming_relationship_is_accepted_and_flagged()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeA = await fx.SeedType(subjectId, "TA");
        var typeB = await fx.SeedType(subjectId, "TB");
        await fx.CreateTemplate().Handle(new CreateConceptChainTemplateCommand(
            subjectId, "CH", "Chain", new[] { typeA, typeB }, Jan1), default);
        var a = await fx.SeedNode(subjectId, typeA, "A");
        var b = await fx.SeedNode(subjectId, typeB, "B");
        var c = await fx.SeedNode(subjectId, typeA, "C"); // second typeA node, so a non-conforming edge stays acyclic

        // Conforming: typeA → typeB is an adjacent pair in the template.
        var conforming = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, a, b, ConceptRelationshipTypes.LeadsTo, "RC", "RC", Jan1, Status: ConceptStatuses.Active),
            default);
        Assert.True(conforming.StatusCode == 201, string.Join("; ", conforming.Errors ?? new List<string>()));

        // Non-conforming: typeB → typeA is NOT in the template; b → c is acyclic (a → b → c is a chain, not a loop).
        var nonConforming = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, b, c, ConceptRelationshipTypes.LeadsTo, "RN", "RN", Jan1, Status: ConceptStatuses.Active),
            default);
        Assert.True(nonConforming.StatusCode == 201, string.Join("; ", nonConforming.Errors ?? new List<string>()));

        var list = await fx.ListRels().Handle(new ListConceptRelationshipsQuery(subjectId), default);
        Assert.True(list.Data!.Items.Single(x => x.RelationshipCode == "RC").IsTemplateConforming);
        Assert.False(list.Data!.Items.Single(x => x.RelationshipCode == "RN").IsTemplateConforming);
    }

    // ---------------- template rules ----------------

    [Fact] // 23  V12 min-2
    public async Task Template_with_single_type_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var t1 = await fx.SeedType(subjectId);
        var r = await fx.CreateTemplate().Handle(new CreateConceptChainTemplateCommand(
            subjectId, "CH", "Chain", new[] { t1 }, Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 24  V12 repeated type
    public async Task Template_with_repeated_type_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var t1 = await fx.SeedType(subjectId);
        var r = await fx.CreateTemplate().Handle(new CreateConceptChainTemplateCommand(
            subjectId, "CH", "Chain", new[] { t1, t1 }, Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 25  V12 foreign-subject type
    public async Task Template_with_foreign_subject_type_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectA = fx.SeedSubject();
        var subjectB = fx.SeedSubject();
        var t1 = await fx.SeedType(subjectA, "T1");
        var foreign = await fx.SeedType(subjectB, "T2");
        var r = await fx.CreateTemplate().Handle(new CreateConceptChainTemplateCommand(
            subjectA, "CH", "Chain", new[] { t1, foreign }, Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 26  V13 overlapping published
    public async Task Two_published_overlapping_versions_return_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var t1 = await fx.SeedType(subjectId, "T1");
        var t2 = await fx.SeedType(subjectId, "T2");
        var first = await fx.CreateTemplate().Handle(new CreateConceptChainTemplateCommand(
            subjectId, "CH", "V1", new[] { t1, t2 }, Jan1, Status: ConceptChainStatuses.Published), default);
        Assert.Equal(201, first.StatusCode);
        var second = await fx.CreateTemplate().Handle(new CreateConceptChainTemplateCommand(
            subjectId, "CH", "V2", new[] { t1, t2 }, Jun1, Status: ConceptChainStatuses.Published), default);
        Assert.Equal(409, second.StatusCode);
    }

    // ---------------- tenant isolation & effective / archived guards ----------------

    [Fact] // 27  tenant isolation — other tenant's type invisible
    public async Task Other_tenant_type_is_invisible()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        await fx.SeedType(subjectId, "A-only");
        var listFromB = await fx.ListTypes(TenantB).Handle(new ListConceptTypesQuery(subjectId), default);
        Assert.Empty(listFromB.Data!.Items);
    }

    [Fact] // 28  tenant isolation — cannot relate another tenant's nodes
    public async Task Cannot_relate_nodes_from_another_tenant()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        // Same fixture but a handler resolved for TenantB cannot see TenantA nodes.
        var relB = new CreateConceptRelationshipHandler(
            Tenant(TenantB), new NullActorContext(), fx.Relationships, fx.Nodes, fx.Templates);
        var r = await relB.Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R", "R", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 29  V15 archived update
    public async Task Archived_type_update_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        await fx.ArchiveType().Handle(new ArchiveConceptTypeCommand(typeId), default);
        var r = await fx.UpdateType().Handle(new UpdateConceptTypeCommand(typeId, "X"), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact] // 30  V14 effective window
    public async Task Node_effective_to_before_from_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var r = await fx.CreateNode().Handle(new CreateConceptNodeCommand(
            subjectId, typeId, "n", "N", Jun1, EffectiveTo: Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- V17 content dirty-check ----------------

    [Fact] // 31  V17 live node binds
    public async Task Content_with_live_concept_node_is_accepted()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var nodeId = await fx.SeedNode(subjectId, typeId);
        var r = await fx.CreateContent().Handle(new CreateKnowledgeContentCommand(
            "KC-A", "Title", KnowledgeContentTypes.Presentation, subjectId, "en", "1.0", Jan1,
            ConceptNodeId: nodeId, Url: "https://x.test"), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact] // 32  V17 archived node rejected
    public async Task Content_with_archived_concept_node_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var nodeId = await fx.SeedNode(subjectId, typeId);
        await fx.ArchiveNode().Handle(new ArchiveConceptNodeCommand(nodeId), default);
        var r = await fx.CreateContent().Handle(new CreateKnowledgeContentCommand(
            "KC-B", "Title", KnowledgeContentTypes.Presentation, subjectId, "en", "1.0", Jan1,
            ConceptNodeId: nodeId, Url: "https://x.test"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 33  V22 dirty-check — untouched dangling node value does not 400 on save
    public async Task Update_content_without_changing_dangling_node_succeeds()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var dangling = Guid.NewGuid(); // not a live node
        var contentId = fx.SeedContent(subjectId, conceptNodeId: dangling);
        var r = await fx.UpdateContent().Handle(new UpdateKnowledgeContentCommand(
            contentId, "Edited title", KnowledgeContentTypes.Presentation, subjectId, "en", "1.0", Jan1,
            ConceptNodeId: dangling, Url: "https://x.test"), default);
        Assert.True(r.IsSuccessful);
    }

    [Fact] // 34  V22 dirty-check — changing to an archived node DOES 400
    public async Task Update_content_changing_to_archived_node_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var nodeId = await fx.SeedNode(subjectId, typeId);
        await fx.ArchiveNode().Handle(new ArchiveConceptNodeCommand(nodeId), default);
        var contentId = fx.SeedContent(subjectId, conceptNodeId: null);
        var r = await fx.UpdateContent().Handle(new UpdateKnowledgeContentCommand(
            contentId, "T", KnowledgeContentTypes.Presentation, subjectId, "en", "1.0", Jan1,
            ConceptNodeId: nodeId, Url: "https://x.test"), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- link rules ----------------

    [Fact] // 35  V18 archived content / node
    public async Task Link_on_archived_content_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var nodeId = await fx.SeedNode(subjectId, typeId);
        var contentId = fx.SeedContent(subjectId, archived: true);
        var r = await fx.CreateLink().Handle(
            new CreateKnowledgeContentConceptLinkCommand(contentId, nodeId), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact] // 36  V21 relationship must contain the anchored node
    public async Task Link_relationship_not_containing_node_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1");
        var n2 = await fx.SeedNode(subjectId, typeId, "N2");
        var n3 = await fx.SeedNode(subjectId, typeId, "N3");
        var rel = await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R", "R", Jan1, Status: ConceptStatuses.Active),
            default);
        var contentId = fx.SeedContent(subjectId);
        // Anchor node n3 is not part of relationship n1→n2.
        var bad = await fx.CreateLink().Handle(new CreateKnowledgeContentConceptLinkCommand(
            contentId, n3, rel.Data), default);
        Assert.Equal(400, bad.StatusCode);
        // Anchoring on n1 (which the relationship contains) succeeds.
        var ok = await fx.CreateLink().Handle(new CreateKnowledgeContentConceptLinkCommand(
            contentId, n1, rel.Data), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ---------------- contract & graph ----------------

    [Fact] // 37  contract 12 flags true
    public async Task Contract_exposes_twelve_true_flags()
    {
        var handler = new GetConceptGraphContractHandler(Tenant(TenantA));
        var r = await handler.Handle(new GetConceptGraphContractQuery(), default);
        Assert.True(r.IsSuccessful);
        var f = r.Data!.Features;
        Assert.True(f.SupportsSubjectConceptGraph && f.SupportsConfigurableConceptChain && f.SupportsConceptType
            && f.SupportsConceptNode && f.SupportsConceptRelationship && f.SupportsConceptChainTemplate
            && f.SupportsContentConceptLink && f.SupportsArchiveLifecycle && f.SupportsEffectiveDating
            && f.SupportsCycleDetection && f.SupportsTemplateConformanceDiagnostics && f.SupportsContractDrivenUi);
        // The forbidden engine flags are absent — only the 12 boolean capability flags exist on the record.
        Assert.Equal(12, typeof(ConceptGraphFeatureFlags).GetProperties().Count(p => p.PropertyType == typeof(bool)));
        Assert.Equal("global-product", ConceptExternalRefTypes.GlobalProduct);
    }

    [Fact] // 38  AC-GRAPH-DEPTH by-content 2 layers, no third layer
    public async Task By_content_returns_two_layers_only()
    {
        var fx = new Fixture(TenantA);
        var subjectId = fx.SeedSubject();
        var typeId = await fx.SeedType(subjectId);
        var n1 = await fx.SeedNode(subjectId, typeId, "N1"); // layer 0 (linked)
        var n2 = await fx.SeedNode(subjectId, typeId, "N2"); // layer 1 (1-hop from n1)
        var n3 = await fx.SeedNode(subjectId, typeId, "N3"); // layer 2 (must NOT appear)
        await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n1, n2, ConceptRelationshipTypes.LeadsTo, "R1", "R1", Jan1, Status: ConceptStatuses.Active),
            default);
        await fx.CreateRel().Handle(new CreateConceptRelationshipCommand(
            subjectId, n2, n3, ConceptRelationshipTypes.LeadsTo, "R2", "R2", Jan1, Status: ConceptStatuses.Active),
            default);
        var contentId = fx.SeedContent(subjectId);
        await fx.CreateLink().Handle(new CreateKnowledgeContentConceptLinkCommand(contentId, n1), default);

        var graph = await fx.ByContent().Handle(new GetConceptGraphByContentQuery(contentId), default);
        var nodeIds = graph.Data!.Nodes.Select(n => n.ConceptNodeId).ToHashSet();
        Assert.Contains(n1, nodeIds);
        Assert.Contains(n2, nodeIds);
        Assert.DoesNotContain(n3, nodeIds); // third layer never surfaces (fixed depth)
    }

    // ============================================================ in-memory fakes

    private sealed class FakeSubjectRepo : ISubjectRepository
    {
        public List<Subject> Items { get; } = new();
        public Task<Subject?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<Subject>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Subject>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<Subject?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.SubjectCode == code && !x.IsArchived()));
        public Task InsertAsync(Subject e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Subject e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContentRepo : IKnowledgeContentRepository
    {
        public List<KnowledgeContent> Items { get; } = new();
        public Task<KnowledgeContent?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgeContent>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.ContentCode == code && !x.IsArchived()));
        public Task InsertAsync(KnowledgeContent e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(KnowledgeContent e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTypeRepo : IConceptTypeRepository
    {
        public List<ConceptType> Items { get; } = new();
        public Task<ConceptType?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ConceptType>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptType>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<ConceptType>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptType>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == s).ToList());
        public Task<ConceptType?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.SubjectId == s && x.ConceptTypeCode == code && !x.IsArchived()));
        public Task InsertAsync(ConceptType e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptType e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeNodeRepo : IConceptNodeRepository
    {
        public List<ConceptNode> Items { get; } = new();
        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == s).ToList());
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid s, Guid ty, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.SubjectId == s && x.ConceptTypeId == ty
                && x.ConceptNodeCode == code && !x.IsArchived()));
        public Task InsertAsync(ConceptNode e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptNode e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRelationshipRepo : IConceptRelationshipRepository
    {
        public List<ConceptRelationship> Items { get; } = new();
        public Task<ConceptRelationship?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ConceptRelationship>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptRelationship>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<ConceptRelationship>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptRelationship>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == s).ToList());
        public Task InsertAsync(ConceptRelationship e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptRelationship e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTemplateRepo : IConceptChainTemplateRepository
    {
        public List<ConceptChainTemplate> Items { get; } = new();
        public Task<ConceptChainTemplate?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ConceptChainTemplate>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == s).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == s && x.ChainCode == code).ToList());
        public Task InsertAsync(ConceptChainTemplate e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptChainTemplate e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeLinkRepo : IKnowledgeContentConceptLinkRepository
    {
        public List<KnowledgeContentConceptLink> Items { get; } = new();
        public Task<KnowledgeContentConceptLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<KnowledgeContentConceptLink>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgeContentConceptLink>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<KnowledgeContentConceptLink>> ListByContentAsync(Guid t, Guid c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgeContentConceptLink>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.KnowledgeContentId == c).ToList());
        public Task<IReadOnlyList<KnowledgeContentConceptLink>> ListByNodeAsync(Guid t, Guid n, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgeContentConceptLink>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.ConceptNodeId == n).ToList());
        public Task InsertAsync(KnowledgeContentConceptLink e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(KnowledgeContentConceptLink e, CancellationToken ct) => Task.CompletedTask;
    }

    // Topic / AudienceProfile are not exercised by the content-node path; minimal no-op fakes keep the ctor satisfied.
    private sealed class FakeTopicNoop : ITopicRepository
    {
        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)new List<Topic>());
        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)new List<Topic>());
        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
            => Task.FromResult<Topic?>(null);
        public Task InsertAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeProfileNoop : IAudienceProfileRepository
    {
        public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult<AudienceProfile?>(null);
        public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AudienceProfile>)new List<AudienceProfile>());
        public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult<AudienceProfile?>(null);
        public Task InsertAsync(AudienceProfile e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(AudienceProfile e, CancellationToken ct) => Task.CompletedTask;
    }
}
