using System.Net;
using System.Text;
using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Infrastructure;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

public sealed class MdmReferenceValidatorTests
{
    [Fact]
    public async Task MatchingReferenceableCompany_IsValidAndForwardsCallerToken()
    {
        var company = Guid.NewGuid();
        string? forwarded = null;
        var client = Client(request =>
        {
            forwarded = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"data\":{{\"legalEntityId\":\"{company:D}\",\"referenceable\":true}}}}", Encoding.UTF8, "application/json")
            };
        });
        var result = await Validator(client).ValidateAsync(company, default);
        Assert.Equal(ReferenceCheck.Valid, result);
        Assert.Equal("Bearer synthetic-token", forwarded);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ReferenceCheck.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ReferenceCheck.Unavailable)]
    [InlineData(HttpStatusCode.Forbidden, ReferenceCheck.Unavailable)]
    public async Task NonSuccessStatuses_DoNotAuthorize(HttpStatusCode status, ReferenceCheck expected)
    {
        var result = await Validator(Client(_ => new HttpResponseMessage(status))).ValidateAsync(Guid.NewGuid(), default);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task WrongCompanyInSuccessfulResponse_IsNotFound()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"data\":{{\"legalEntityId\":\"{Guid.NewGuid():D}\",\"referenceable\":true}}}}")
        });
        Assert.Equal(ReferenceCheck.NotFound, await Validator(client).ValidateAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task MissingCallerToken_IsUnavailable()
    {
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var validator = new MdmLegalEntityReferenceValidator(Client(_ => throw new InvalidOperationException("must not call MDM")), context);
        Assert.Equal(ReferenceCheck.Unavailable, await validator.ValidateAsync(Guid.NewGuid(), default));
    }

    private static MdmLegalEntityReferenceValidator Validator(HttpClient client)
    {
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        context.HttpContext.Request.Headers.Authorization = "Bearer synthetic-token";
        return new MdmLegalEntityReferenceValidator(client, context);
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new StubHandler(responder)) { BaseAddress = new Uri("http://127.0.0.1:5059/api/legal-entities/") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
