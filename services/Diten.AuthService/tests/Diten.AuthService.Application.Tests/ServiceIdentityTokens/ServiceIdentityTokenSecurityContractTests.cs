using System.Text;
using System.Text.Json;
using Diten.AuthService.Api.Security;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Commands;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Validators;
using Diten.AuthService.Api.Controllers.Internal;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceIdentityTokenSecurityContractTests
{
    [Fact]
    public async Task Parser_accepts_only_exact_two_field_body()
    {
        var tenantId = Guid.NewGuid();
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"tenantId\":\"{tenantId:D}\",\"audience\":\"TRUSTED_AUDIT_SOURCE_INGEST\"}}"));
        var parsed = await ServiceIdentityTokenRequestParser.ParseAsync(body, CancellationToken.None);
        Assert.Equal(tenantId, parsed.TenantId);
        Assert.Equal("TRUSTED_AUDIT_SOURCE_INGEST", parsed.Audience);
    }

    [Theory]
    [InlineData("{\"tenantId\":\"00000000-0000-0000-0000-000000000001\",\"audience\":\"A\",\"extra\":1}")]
    [InlineData("{\"tenantId\":\"00000000-0000-0000-0000-000000000001\",\"tenantId\":\"00000000-0000-0000-0000-000000000001\",\"audience\":\"A\"}")]
    [InlineData("{\"tenantId\":\"00000000-0000-0000-0000-000000000001\",\"audience\":\" A\"}")]
    public async Task Parser_rejects_unknown_duplicate_and_trim_drift(string json)
    {
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await Assert.ThrowsAsync<JsonException>(() => ServiceIdentityTokenRequestParser.ParseAsync(body, CancellationToken.None));
    }

    [Fact]
    public async Task Parser_enforces_stream_limit_without_content_length()
    {
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 1025)));
        await Assert.ThrowsAsync<ServiceIdentityRequestTooLargeException>(() =>
            ServiceIdentityTokenRequestParser.ParseAsync(body, CancellationToken.None));
    }

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("application/json; charset=utf-8", true)]
    [InlineData("text/json", false)]
    [InlineData("application/json; charset=ascii", false)]
    [InlineData(null, false)]
    public void Transport_contract_is_strict(string? contentType, bool expected) =>
        Assert.Equal(expected, ServiceIdentityTokenTransportContract.IsSupportedContentType(contentType));

    [Theory]
    [InlineData("client", 128, true)]
    [InlineData(" client", 128, false)]
    [InlineData("client\n", 128, false)]
    [InlineData("client,other", 128, false)]
    public void Header_contract_rejects_trim_and_control_drift(string value, int max, bool expected) =>
        Assert.Equal(expected, ServiceIdentityTokenTransportContract.IsExactValue(value, max));

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("?tenantId=spoofed", false)]
    public void Query_contract_allows_no_authority_in_url(string? query, bool expected) =>
        Assert.Equal(expected, ServiceIdentityTokenTransportContract.HasNoQuery(query));

    [Theory]
    [InlineData(" client", "secret", "TRUSTED_AUDIT_SOURCE_INGEST")]
    [InlineData("client", "secret\n", "TRUSTED_AUDIT_SOURCE_INGEST")]
    [InlineData("client", "secret", "OTHER")]
    [InlineData("client,other", "secret", "TRUSTED_AUDIT_SOURCE_INGEST")]
    public void Validator_mirrors_transport_boundaries(string client, string secret, string audience)
    {
        var result = new IssueServiceIdentityTokenValidator().Validate(new IssueServiceIdentityTokenCommand(
            client, secret, Guid.NewGuid(), audience));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("TRUSTED_AUDIT_SOURCE_INGEST")]
    [InlineData("TRUSTED_WORKFLOW_CONSUMER")]
    public void Validator_accepts_only_each_exact_bounded_audience(string audience)
    {
        var result = new IssueServiceIdentityTokenValidator().Validate(new IssueServiceIdentityTokenCommand(
            "client", "secret", Guid.NewGuid(), audience));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("trusted_workflow_consumer")]
    [InlineData("TRUSTED_WORKFLOW_CONSUMER ")]
    [InlineData("TRUSTED_WORKFLOW_CONSUMER,TRUSTED_AUDIT_SOURCE_INGEST")]
    public void Validator_rejects_case_trim_and_multi_audience_drift(string audience)
    {
        var result = new IssueServiceIdentityTokenValidator().Validate(new IssueServiceIdentityTokenCommand(
            "client", "secret", Guid.NewGuid(), audience));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Controller_rethrows_caller_cancellation_even_when_pipeline_returns_500_envelope()
    {
        using var cancellation = new CancellationTokenSource();
        var controller = new InternalServiceIdentityTokensController(new CancelThenReturnMediator(cancellation))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.ContentType = "application/json";
        controller.Request.Headers["X-Service-Client-Id"] = "mdm-client";
        controller.Request.Headers["X-Service-Client-Secret"] = "secret";
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            $"{{\"tenantId\":\"{Guid.NewGuid():D}\",\"audience\":\"TRUSTED_AUDIT_SOURCE_INGEST\"}}"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Issue(cancellation.Token));
    }

    private sealed class CancelThenReturnMediator(CancellationTokenSource cancellation) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            object response = Response<ServiceIdentityTokenResponse>.Fail("swallowed cancellation", 500);
            return Task.FromResult((TResponse)response);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
