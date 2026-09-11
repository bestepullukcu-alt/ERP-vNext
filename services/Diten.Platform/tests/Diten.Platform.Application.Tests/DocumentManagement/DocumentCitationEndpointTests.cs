using System.Reflection;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// DCP-005 (WP-DM-2b) — citation HTTP surface tests, mirroring the effectiveness:batch endpoint. The two endpoints are
/// thin screens over the ready DM-2a citation queries: citations:resolve validates request shape (parseable <c>by</c>
/// + a non-blank identifier) and dispatches ResolveDocumentCitationQuery; citations/search dispatches
/// SearchDocumentCitationQuery. Both are gated by CitationRead.
/// </summary>
public sealed class DocumentCitationEndpointTests
{
    private static readonly MethodInfo ResolveAction =
        typeof(DocumentManagementMasterRegisterController).GetMethod(nameof(DocumentManagementMasterRegisterController.ResolveCitations))!;
    private static readonly MethodInfo SearchAction =
        typeof(DocumentManagementMasterRegisterController).GetMethod(nameof(DocumentManagementMasterRegisterController.SearchCitations))!;

    // ── endpoint contract (route + verb + permission) ───────────────────────────

    [Fact]
    public void Resolve_route_and_verb_are_citations_resolve_post()
    {
        var post = ResolveAction.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("document-master-register/citations:resolve", post!.Template);
    }

    [Fact]
    public void Search_route_and_verb_are_citations_search_get()
    {
        var get = SearchAction.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(get);
        Assert.Equal("document-master-register/citations/search", get!.Template);
    }

    [Fact]
    public void Both_endpoints_are_gated_by_the_citation_read_permission()
    {
        foreach (var action in new[] { ResolveAction, SearchAction })
        {
            var permissions = action.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission).ToList();
            Assert.Equal("platform.document-management.master-register.citation.read", Assert.Single(permissions));
            Assert.Equal(DocumentMasterRegisterPermissions.CitationRead, permissions[0]);
        }
    }

    // ── behaviour ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_happy_path_dispatches_the_query_and_returns_200()
    {
        var expected = new DocumentCitationResult(new[]
        {
            new DocumentCitationItem("UID-1", "C-1", "Doc", "V1.0", "Effective", true, null, DocumentCitationSource.MasterRegister, Guid.NewGuid())
        });
        var mediator = new Mock<IMediator>();
        ResolveDocumentCitationQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<ResolveDocumentCitationQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => captured = (ResolveDocumentCitationQuery)q)
            .ReturnsAsync(Response<DocumentCitationResult>.Success(expected, 200, "t-corr"));
        var controller = NewController(mediator);

        var result = await controller.ResolveCitations(
            new ResolveCitationApiRequest { By = "uid", Identifiers = new[] { "UID-1" } }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<Response<DocumentCitationResult>>(ok.Value);
        Assert.True(body.IsSuccessful);
        Assert.NotNull(captured);
        Assert.Equal(DocumentIdentifierKind.Uid, captured!.By);
        Assert.Equal(new[] { "UID-1" }, captured.Identifiers);
    }

    [Fact]
    public async Task Resolve_empty_identifiers_returns_400_without_dispatching()
    {
        var mediator = new Mock<IMediator>();
        var controller = NewController(mediator);

        var result = await controller.ResolveCitations(
            new ResolveCitationApiRequest { By = "uid", Identifiers = Array.Empty<string>() }, CancellationToken.None);

        AssertInvalidRequest(result);
        mediator.Verify(m => m.Send(It.IsAny<ResolveDocumentCitationQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("name")]
    [InlineData("0")]
    public async Task Resolve_missing_or_invalid_by_returns_400_without_dispatching(string? by)
    {
        var mediator = new Mock<IMediator>();
        var controller = NewController(mediator);

        var result = await controller.ResolveCitations(
            new ResolveCitationApiRequest { By = by, Identifiers = new[] { "UID-1" } }, CancellationToken.None);

        AssertInvalidRequest(result);
        mediator.Verify(m => m.Send(It.IsAny<ResolveDocumentCitationQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_dispatches_the_query_with_term_and_limit_and_returns_200()
    {
        var mediator = new Mock<IMediator>();
        SearchDocumentCitationQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<SearchDocumentCitationQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => captured = (SearchDocumentCitationQuery)q)
            .ReturnsAsync(Response<DocumentCitationResult>.Success(new DocumentCitationResult([]), 200, "t-corr"));
        var controller = NewController(mediator);

        var result = await controller.SearchCitations("sop", 25, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<Response<DocumentCitationResult>>(ok.Value);
        Assert.NotNull(captured);
        Assert.Equal("sop", captured!.Term);
        Assert.Equal(25, captured.Limit);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static void AssertInvalidRequest(IActionResult result)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<Response<DocumentCitationResult>>(bad.Value);
        Assert.False(body.IsSuccessful);
        Assert.Equal(400, body.StatusCode);
        Assert.Equal("invalid_request", body.ReasonCode);
    }

    private static DocumentManagementMasterRegisterController NewController(Mock<IMediator> mediator)
    {
        var correlation = new Mock<ICorrelationContext>();
        correlation.SetupGet(c => c.CorrelationId).Returns("t-corr");
        return new DocumentManagementMasterRegisterController(mediator.Object, correlation.Object);
    }
}
