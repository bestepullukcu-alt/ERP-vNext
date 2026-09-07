using System.Text;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.Workflow;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class TrustedWorkflowCancellationContractTests
{
    [Fact]
    public void Strict_parser_accepts_exact_preflight_and_cancel_shapes()
    {
        var parser = new TrustedWorkflowConsumerRequestParser();
        var instanceId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var makerId = Guid.NewGuid();

        Assert.True(parser.TryParseCancellationPreflight(Encoding.UTF8.GetBytes(
            $$"""{"workflowInstanceId":"{{instanceId:D}}","approvalTaskId":"{{taskId:D}}","expectedObjectType":"GlobalProduct","expectedObjectId":"GP-1","expectedMakerSubjectId":"{{makerId:D}}"}"""), out var preflight));
        Assert.NotNull(preflight);

        Assert.True(parser.TryParseCancellation(Encoding.UTF8.GetBytes(
            $$"""{"workflowInstanceId":"{{instanceId:D}}","approvalTaskId":"{{taskId:D}}","expectedObjectType":"GlobalProduct","expectedObjectId":"GP-1","expectedMakerSubjectId":"{{makerId:D}}","expectedWorkflowInstanceVersion":5,"expectedApprovalTaskVersion":1,"reasonCode":"WITHDRAW","comment":"requested"}"""), out var cancel));
        Assert.NotNull(cancel);
        Assert.Equal(5, cancel.ExpectedWorkflowInstanceVersion);
    }

    [Theory]
    [InlineData("\"tenantId\":\"11111111-1111-1111-1111-111111111111\",")]
    [InlineData("\"actorId\":\"11111111-1111-1111-1111-111111111111\",")]
    [InlineData("\"expectedWorkflowInstanceVersion\":0,")]
    [InlineData("\"unexpected\":true,")]
    public void Strict_cancel_parser_rejects_authority_unknown_and_non_positive_version(string injected)
    {
        var json = $$"""{"workflowInstanceId":"{{Guid.NewGuid():D}}","approvalTaskId":"{{Guid.NewGuid():D}}","expectedObjectType":"GlobalProduct","expectedObjectId":"GP-1","expectedMakerSubjectId":"{{Guid.NewGuid():D}}",{{injected}}"expectedWorkflowInstanceVersion":5,"expectedApprovalTaskVersion":1,"reasonCode":"WITHDRAW"}""";

        Assert.False(new TrustedWorkflowConsumerRequestParser().TryParseCancellation(
            Encoding.UTF8.GetBytes(json), out _));
    }

    [Fact]
    public void Controller_exposes_only_dedicated_trusted_consumer_cancellation_routes()
    {
        var controllerType = typeof(InternalTrustedWorkflowConsumerController);
        var controllerRoute = Assert.Single(controllerType.GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>());
        var authorize = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());
        var preflight = controllerType.GetMethod(nameof(InternalTrustedWorkflowConsumerController.GetCancellationPreflight));
        var cancel = controllerType.GetMethod(nameof(InternalTrustedWorkflowConsumerController.Cancel));

        Assert.Equal("api/internal/v1/workflow/trusted-consumer", controllerRoute.Template);
        Assert.Equal(
            TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme,
            authorize.AuthenticationSchemes);
        Assert.Equal("cancel-preflight", Assert.Single(preflight!.GetCustomAttributes(typeof(HttpPostAttribute), false)
            .Cast<HttpPostAttribute>()).Template);
        Assert.Equal("cancel", Assert.Single(cancel!.GetCustomAttributes(typeof(HttpPostAttribute), false)
            .Cast<HttpPostAttribute>()).Template);
    }
}
