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
/// DCP-005 (P-EFF-P2 Faz 3) — effectiveness:batch HTTP endpoint tests. The controller is a thin screen over the single
/// resolver: it validates request shape (parseable <c>by</c> + at least one non-blank identifier), dispatches, and
/// treats <c>Unresolved</c> as a 200 result — a 400 is emitted only for a malformed request (contract §4/§5).
/// </summary>
public sealed class DocumentEffectivenessEndpointTests
{
    private static readonly MethodInfo Action =
        typeof(DocumentManagementMasterRegisterController).GetMethod(nameof(DocumentManagementMasterRegisterController.ResolveEffectiveness))!;

    // ── endpoint contract (route + permission) ──────────────────────────────────

    [Fact]
    public void Endpoint_route_and_verb_are_effectiveness_batch_post()
    {
        var post = Action.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("document-master-register/effectiveness:batch", post!.Template);
    }

    [Fact]
    public void Endpoint_is_gated_by_the_effectiveness_read_permission()
    {
        var permissions = Action.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission).ToList();
        Assert.Contains("platform.document-management.master-register.effectiveness.read", permissions);
        Assert.Equal(DocumentMasterRegisterPermissions.EffectivenessRead, Assert.Single(permissions));
    }

    // ── behaviour ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Happy_path_dispatches_the_query_and_returns_200()
    {
        var expected = new DocumentEffectivenessResult(new[]
        {
            new DocumentEffectivenessItem("UID-0000104", DocumentEffectivenessState.Effective, "C1", "UID-0000104", "Effective", null)
        });
        var mediator = new Mock<IMediator>();
        ResolveDocumentEffectivenessQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<ResolveDocumentEffectivenessQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => captured = (ResolveDocumentEffectivenessQuery)q)
            .ReturnsAsync(Response<DocumentEffectivenessResult>.Success(expected, 200, "t-corr"));
        var controller = NewController(mediator);

        var result = await controller.ResolveEffectiveness(
            new ResolveEffectivenessApiRequest { By = "uid", Identifiers = new[] { "UID-0000104" } }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<Response<DocumentEffectivenessResult>>(ok.Value);
        Assert.True(body.IsSuccessful);
        Assert.Equal(200, body.StatusCode);
        // The resolver received the typed kind and the identifiers (single-resolver dispatch).
        Assert.NotNull(captured);
        Assert.Equal(DocumentIdentifierKind.Uid, captured!.By);
        Assert.Equal(new[] { "UID-0000104" }, captured.Identifiers);
        mediator.Verify(m => m.Send(It.IsAny<ResolveDocumentEffectivenessQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unresolved_item_is_not_an_error_and_still_returns_200()
    {
        var withUnresolved = new DocumentEffectivenessResult(new[]
        {
            new DocumentEffectivenessItem("UID-MISSING", DocumentEffectivenessState.Unresolved, null, null, null, null)
        });
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ResolveDocumentEffectivenessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<DocumentEffectivenessResult>.Success(withUnresolved, 200, "t-corr"));
        var controller = NewController(mediator);

        var result = await controller.ResolveEffectiveness(
            new ResolveEffectivenessApiRequest { By = "code", Identifiers = new[] { "UID-MISSING" } }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<Response<DocumentEffectivenessResult>>(ok.Value);
        Assert.True(body.IsSuccessful);
        Assert.Equal(DocumentEffectivenessState.Unresolved, Assert.Single(body.Data!.Items).State);
    }

    [Fact]
    public async Task Empty_identifiers_returns_400_without_dispatching()
    {
        var mediator = new Mock<IMediator>();
        var controller = NewController(mediator);

        var result = await controller.ResolveEffectiveness(
            new ResolveEffectivenessApiRequest { By = "uid", Identifiers = Array.Empty<string>() }, CancellationToken.None);

        AssertInvalidRequest(result);
        mediator.Verify(m => m.Send(It.IsAny<ResolveDocumentEffectivenessQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task All_whitespace_identifiers_returns_400_without_dispatching()
    {
        var mediator = new Mock<IMediator>();
        var controller = NewController(mediator);

        var result = await controller.ResolveEffectiveness(
            new ResolveEffectivenessApiRequest { By = "uid", Identifiers = new[] { "  ", "" } }, CancellationToken.None);

        AssertInvalidRequest(result);
        mediator.Verify(m => m.Send(It.IsAny<ResolveDocumentEffectivenessQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("name")]   // unknown discriminator
    [InlineData("0")]      // numeric enum value must NOT be silently accepted
    public async Task Missing_or_invalid_by_returns_400_without_dispatching(string? by)
    {
        var mediator = new Mock<IMediator>();
        var controller = NewController(mediator);

        var result = await controller.ResolveEffectiveness(
            new ResolveEffectivenessApiRequest { By = by, Identifiers = new[] { "UID-0000104" } }, CancellationToken.None);

        AssertInvalidRequest(result);
        mediator.Verify(m => m.Send(It.IsAny<ResolveDocumentEffectivenessQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static void AssertInvalidRequest(IActionResult result)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<Response<DocumentEffectivenessResult>>(bad.Value);
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
