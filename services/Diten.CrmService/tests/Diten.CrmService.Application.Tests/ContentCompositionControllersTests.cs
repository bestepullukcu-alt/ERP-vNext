using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-KP-4 retired the content set, the set revision and the content scope (DESIGN-KP-STUDIO §7, bridge-decision §7 /
/// §8); WP-CLN-1 removed their read-only remnants. Pins: no controller, query, entity, repository or class map of the
/// three is left; the artifact store lives in the shared Common.Artifacts seam with the knowledge path renderer; and
/// what the clean-up had to KEEP still reads — the release provenance on knowledge content / paths
/// (<see cref="KnowledgeStudioOrigin"/>, same BSON shape) and the SCMM-13 language-variant group
/// (<see cref="KnowledgeContent.ContentSetId"/>, not a content set).
/// </summary>
public sealed class ContentCompositionControllersTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ================================================================ removed

    [Fact]
    public void The_content_set_revision_and_scope_code_is_gone()
    {
        var assemblies = new[]
        {
            typeof(Diten.CrmService.Api.Controllers.CRM.ClaimsController).Assembly,
            typeof(IContentArtifactStore).Assembly,
            typeof(KnowledgeStudioOrigin).Assembly,
            typeof(Diten.CrmService.Persistence.DependencyInjection).Assembly,
            typeof(Diten.CrmService.Infrastructure.Artifacts.HttpContentArtifactStore).Assembly
        };
        var names = assemblies.SelectMany(a => a.GetTypes()).Select(t => t.Name).ToHashSet();

        foreach (var gone in new[]
                 {
                     "ContentSetsController", "ContentSetRevisionsController", "ContentScopesController",
                     "ContentSet", "ContentSetRevision", "ContentScope", "ContentSetScopeRef", "ContentArrangement",
                     "IContentSetRepository", "IContentSetRevisionRepository", "IContentScopeRepository",
                     "ContentSetRepository", "ContentSetRevisionRepository", "ContentScopeRepository",
                     "ListContentSetsQuery", "GetContentSetQuery", "ListContentSetRevisionsQuery",
                     "GetContentSetRevisionArtifactQuery", "IContentSetContextResolver", "ContentSetContextErrors",
                     "ContentSetPermissions", "ContentSetRevisionPermissions"
                 })
        {
            Assert.DoesNotContain(gone, names);
        }

        // The shared chain-context codes the knowledge path uses survived the move out of ContentSet.cs.
        Assert.Equal("component_language_mismatch", ChainContextErrors.ComponentLanguageMismatch);
    }

    [Fact]
    public void No_class_map_is_registered_for_a_removed_type()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();

        Assert.DoesNotContain(BsonClassMap.GetRegisteredClassMaps(), m =>
            m.ClassType.Name.StartsWith("ContentSet", StringComparison.Ordinal)
            || m.ClassType.Name.StartsWith("ContentScope", StringComparison.Ordinal));
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

    // ================================================================ kept

    [Fact]
    public void Release_provenance_keeps_its_bson_shape_and_an_old_document_still_reads()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var origin = new KnowledgeStudioOrigin
        {
            ContentSetId = Guid.NewGuid(), ContentSetRevisionId = Guid.NewGuid(),
            ConceptChainTemplateId = Guid.NewGuid(), ChainVersion = "1.2"
        };
        var content = new KnowledgeContent { TenantId = TenantA, ContentCode = "C-1", StudioOrigin = origin };
        var path = new KnowledgePath { TenantId = TenantA, PathCode = "P-1", StudioOrigin = origin };

        foreach (var doc in new[] { content.ToBsonDocument(), path.ToBsonDocument() })
        {
            var stored = doc["StudioOrigin"].AsBsonDocument;
            Assert.Equal(
                new[] { "ContentSetId", "ContentSetRevisionId", "ConceptChainTemplateId", "ChainVersion" },
                stored.Names);
            Assert.All(new[] { "ContentSetId", "ContentSetRevisionId", "ConceptChainTemplateId" },
                f => Assert.Equal(BsonType.String, stored[f].BsonType));
        }

        // A document a WP-SB-2 release wrote (string ids, as stored then) still reads after the set code is gone.
        var old = content.ToBsonDocument();
        old["StudioOrigin"] = new BsonDocument
        {
            { "ContentSetId", origin.ContentSetId.ToString() },
            { "ContentSetRevisionId", origin.ContentSetRevisionId.ToString() },
            { "ConceptChainTemplateId", origin.ConceptChainTemplateId.ToString() },
            { "ChainVersion", "1.2" }
        };
        var back = BsonSerializer.Deserialize<KnowledgeContent>(old).StudioOrigin!;
        Assert.Equal(
            (origin.ContentSetId, origin.ContentSetRevisionId, origin.ConceptChainTemplateId, "1.2"),
            (back.ContentSetId, back.ContentSetRevisionId, back.ConceptChainTemplateId, back.ChainVersion));
        Assert.Equal(origin.ContentSetRevisionId,
            BsonSerializer.Deserialize<KnowledgePath>(path.ToBsonDocument()).StudioOrigin!.ContentSetRevisionId);
    }

    [Fact]
    public void The_language_variant_group_on_knowledge_content_round_trips_as_a_string()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var group = Guid.NewGuid();
        var doc = new KnowledgeContent { TenantId = TenantA, ContentCode = "C-2", ContentSetId = group }.ToBsonDocument();

        Assert.Equal(BsonType.String, doc["ContentSetId"].BsonType);
        Assert.Equal(group, BsonSerializer.Deserialize<KnowledgeContent>(doc).ContentSetId);
    }
}
