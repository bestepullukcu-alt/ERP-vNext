using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Application.Features.ContentComposition.ContentSets;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-KP-4 (DESIGN-KP-STUDIO §7, bridge-decision §7 / §8) — the content set and the content scope are retired; the
/// knowledge path studio took their job. Pins: the set / set-revision controllers expose only GET reads (no create /
/// clone / edit / component / claim / eligibility / archive, no submit / review / render / release / withdraw), the
/// scope controller and its reads are gone, the set write commands + the SB-2 release producer + the set renderer no
/// longer exist; the reads still work on old data (a pre-SB-1R document with the retired "Scope" element still parses as
/// LegacyScope, and the set read still resolves its context); an old revision's rendered artifact still streams; the
/// artifact store lives in the shared Common.Artifacts seam (the knowledge path renderer + store are the only render
/// registrations).
/// </summary>
public sealed class ContentCompositionControllersTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private const string SetBase = "api/crm/content-composition/content-sets";
    private const string RevisionBase = "api/crm/content-composition/content-set-revisions";

    // ================================================================ HTTP surface

    [Fact]
    public void The_set_and_revision_controllers_are_read_only_and_authorized()
    {
        foreach (var controller in new[] { typeof(ContentSetsController), typeof(ContentSetRevisionsController) })
        {
            Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
            var verbs = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods))
                .Distinct()
                .ToList();
            Assert.Equal(["GET"], verbs);
        }
    }

    [Theory]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.List), SetBase)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.Get), SetBase + "/{contentSetId:guid}")]
    [InlineData(typeof(ContentSetRevisionsController), nameof(ContentSetRevisionsController.List), RevisionBase)]
    [InlineData(typeof(ContentSetRevisionsController), nameof(ContentSetRevisionsController.Get), RevisionBase + "/{revisionId:guid}")]
    [InlineData(typeof(ContentSetRevisionsController), nameof(ContentSetRevisionsController.Artifact), RevisionBase + "/{revisionId:guid}/artifact")]
    public void Each_remaining_read_keeps_its_route_and_the_read_key_and_is_marked_obsolete(Type controller, string action, string route)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;
        Assert.Equal(route, method.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("crm.content-set.read", method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
        Assert.NotNull(method.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void The_retired_write_surface_and_the_scope_reads_no_longer_exist()
    {
        var api = typeof(ContentSetsController).Assembly;
        var application = typeof(ListContentSetsQuery).Assembly;
        var infrastructure = typeof(Diten.CrmService.Infrastructure.Artifacts.HttpContentArtifactStore).Assembly;
        Assert.Null(api.GetTypes().FirstOrDefault(t => t.Name == "ContentScopesController"));
        foreach (var gone in new[]
                 {
                     "CreateContentSetDraftCommand", "CloneContentSetCommand", "UpdateContentSetDraftCommand", "ArchiveContentSetCommand",
                     "AddContentSetComponentCommand", "AddContentSetClaimCommand", "ApplyContentSetEligibilityCommand",
                     "SubmitContentSetForReviewCommand", "RecordReviewDecisionCommand", "RenderContentSetRevisionCommand",
                     "ReleaseContentSetRevisionCommand", "WithdrawContentSetRevisionCommand",
                     "IContentSetReleaseProducer", "ContentSetReleaseProducer", "ContentSetPathOrder", "ContentSetContextValidation",
                     "IContentSetRevisionRenderer", "ListContentScopesQuery", "GetContentScopeQuery", "ContentScopeDto"
                 })
        {
            Assert.Null(application.GetTypes().FirstOrDefault(t => t.Name == gone));
        }

        Assert.Null(infrastructure.GetTypes().FirstOrDefault(t => t.Name == "PdfSharpContentSetRevisionRenderer"));
        // Only the read keys remain enforced by the set code.
        Assert.Equal(["crm.content-set.read"], ContentSetPermissions.All);
    }

    [Fact]
    public void The_artifact_store_moved_to_the_shared_seam_and_only_the_knowledge_path_renderer_is_registered()
    {
        Assert.Equal("Diten.CrmService.Application.Common.Artifacts", typeof(IContentArtifactStore).Namespace);
        Assert.Equal("Diten.CrmService.Application.Common.Artifacts", typeof(RenderedContent).Namespace);

        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        Diten.CrmService.Infrastructure.DependencyInjection.AddInfrastructure(services,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GatewayUrl"] = "http://gateway.test", ["Jwt:Key"] = "k", ["Jwt:Issuer"] = "i", ["Jwt:Audience"] = "a"
            }).Build());
        Assert.Contains(services, d => d.ServiceType == typeof(IContentArtifactStore));
        Assert.Contains(services, d => d.ServiceType == typeof(Diten.CrmService.Application.Features.Knowledge.Path.Release.IKnowledgePathRevisionRenderer));
        Assert.DoesNotContain(services, d => d.ServiceType.Name.Contains("ContentSet", StringComparison.Ordinal));
    }

    // ================================================================ reads still work on old data

    [Fact]
    public void A_pre_sb1r_set_and_revision_with_the_retired_scope_element_still_read()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var legacyScope = new BsonDocument { { "ContentScopeId", Guid.NewGuid().ToString() }, { "ScopeVersion", "1.0" } };

        var setDoc = new ContentSet { TenantId = TenantA, SetCode = "SET-OLD", SetName = "Old" }.ToBsonDocument();
        setDoc.Remove("CountryCode");
        setDoc.Remove("LanguageCode");
        setDoc["Scope"] = legacyScope;
        var set = BsonSerializer.Deserialize<ContentSet>(setDoc);           // must not throw on the retired element
        Assert.Equal("SET-OLD", set.SetCode);

        var revisionDoc = new ContentSetRevision { TenantId = TenantA, RevisionCode = "SET-OLD-R1" }.ToBsonDocument();
        revisionDoc.Remove("Context");
        revisionDoc["Scope"] = legacyScope.DeepClone();
        var revision = BsonSerializer.Deserialize<ContentSetRevision>(revisionDoc);
#pragma warning disable CS0618 // the legacy element is kept for read-compatibility only
        Assert.Equal("1.0", set.LegacyScope!.ScopeVersion);
        Assert.Equal("1.0", revision.LegacyScope!.ScopeVersion);
#pragma warning restore CS0618
        Assert.Equal("SET-OLD-R1", ContentSetRevisionMapper.ToDto(revision).RevisionCode);
    }

    [Fact]
    public async Task The_set_reads_still_list_and_resolve_an_old_set_with_its_context()
    {
        var templates = new ContentSetTestTemplates();
        var subjects = new ContentSetTestSubjects();
        var profiles = new ContentSetTestProfiles();
        var sets = new ContentSetTestSets();
        var subject = new Subject
        {
            TenantId = TenantA, SubjectCode = "ALM", SubjectName = "Almiba",
            ExternalReferences = { new KnowledgeExternalReference { SourceSystem = "global-product", ExternalId = ProductX.ToString(), ExternalCode = "ALMIBA", ExternalName = "Almiba 1 g", IsPrimary = true } }
        };
        subjects.Items.Add(subject);
        var template = new ConceptChainTemplate { TenantId = TenantA, ChainCode = "TPL", ChainName = "T", SubjectId = subject.Id, ChainVersion = "1.0" };
        templates.Items.Add(template);
        var old = new ContentSet
        {
            TenantId = TenantA, SetCode = "SET-1", SetName = "Old set", CountryCode = "TR", LanguageCode = "tr",
            Template = new ContentSetTemplateRef { ConceptChainTemplateId = template.Id, ChainVersion = "1.0" }
        };
        sets.Items.Add(old);
        var resolver = new ContentSetContextResolver(templates, subjects, profiles);

        var list = (await new ListContentSetsHandler(Tenant(), sets, resolver).Handle(new ListContentSetsQuery(), default)).Data!;
        Assert.Equal("SET-1", Assert.Single(list.Items).SetCode);
        var one = (await new GetContentSetHandler(Tenant(), sets, resolver).Handle(new GetContentSetQuery(old.Id), default)).Data!;
        Assert.Equal(("TR", "ALMIBA"), (one.Context!.CountryCode, one.Context.ProductCode));
    }

    [Fact]
    public async Task An_old_revision_and_its_rendered_artifact_still_read_and_another_tenant_gets_404()
    {
        var revisions = new ContentSetTestRevisions();
        var contentId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var rendered = new ContentSetRevision
        {
            TenantId = TenantA, ContentSetId = setId, RevisionCode = "SET-1-R1",
            RenderedArtifact = new ContentSetRenderedArtifact { ContentId = contentId, MediaType = "application/pdf" }
        };
        revisions.Items.Add(rendered);
        var store = new ReadOnlyStore(contentId);

        var list = (await new ListContentSetRevisionsHandler(Tenant(), revisions).Handle(new ListContentSetRevisionsQuery(setId), default)).Data!;
        Assert.Equal("SET-1-R1", Assert.Single(list.Items).RevisionCode);
        var artifact = await new GetContentSetRevisionArtifactHandler(Tenant(), revisions, store)
            .Handle(new GetContentSetRevisionArtifactQuery(rendered.Id), default);
        Assert.True(artifact.IsSuccessful);
        Assert.Equal("application/pdf", artifact.Data!.MediaType);

        var other = new TenantContext();
        other.SetTenant(Guid.NewGuid());
        var foreign = await new GetContentSetRevisionArtifactHandler(other, revisions, store)
            .Handle(new GetContentSetRevisionArtifactQuery(rendered.Id), default);
        Assert.Equal(404, foreign.StatusCode);
    }

    private static TenantContext Tenant()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantA);
        return tenant;
    }

    private sealed class ReadOnlyStore : IContentArtifactStore
    {
        private readonly Guid _contentId;
        public ReadOnlyStore(Guid contentId) => _contentId = contentId;

        public Task<ContentArtifactStoreResult> StoreAsync(ContentArtifactStoreRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The retired content set never stores.");

        public Task<ContentArtifactReadResult?> OpenReadAsync(Guid contentId, CancellationToken cancellationToken)
            => Task.FromResult(contentId == _contentId
                ? new ContentArtifactReadResult(new MemoryStream([0x25, 0x50, 0x44, 0x46]), "application/pdf", "old.pdf", 4)
                : null);
    }
}
