using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Quotas;

/// <summary>
/// BL-459 — <c>POST /api/internal/quotas/consume</c> refusing at the limit now says what the limit is (the envelope's
/// fields plus <c>quota: { quotaKey, limitValue, currentValue }</c>); every other answer is the unchanged Response.
/// </summary>
public sealed class InternalQuotasConsumeLimitTests
{
    private const string Key = "test-internal-key";
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task A_refusal_at_the_limit_carries_the_limit_and_the_usage()
    {
        var quota = new Mock<IQuotaService>();
        quota.Setup(q => q.GetStatusResponseAsync(Tenant, QuotaKeys.UsersMax, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<QuotaStatusDto>.Success(Status(limit: 10, current: 10)));
        var controller = Controller(Response<QuotaMutationDto>.Fail(QuotaErrorCodes.LimitExceeded, 409), quota.Object);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Consume(Request(), CancellationToken.None));

        var body = Assert.IsType<QuotaLimitExceededEnvelope>(result.Value);
        Assert.False(body.IsSuccessful);
        Assert.Equal(409, body.StatusCode);
        Assert.Equal([QuotaErrorCodes.LimitExceeded], body.Errors);
        Assert.Equal(new QuotaLimitSnapshot(QuotaKeys.UsersMax, 10, 10), body.Quota);
    }

    [Fact]
    public async Task Any_other_conflict_is_the_plain_response_and_reads_no_status()
    {
        var quota = new Mock<IQuotaService>(MockBehavior.Strict);
        var controller = Controller(Response<QuotaMutationDto>.Fail(QuotaErrorCodes.DuplicateOperation, 409), quota.Object);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Consume(Request(), CancellationToken.None));

        Assert.IsType<Response<QuotaMutationDto>>(result.Value);
    }

    [Fact]
    public async Task When_the_status_cannot_be_read_the_plain_refusal_goes_out()
    {
        var quota = new Mock<IQuotaService>();
        quota.Setup(q => q.GetStatusResponseAsync(Tenant, QuotaKeys.UsersMax, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<QuotaStatusDto>.Fail(QuotaErrorCodes.UsageNotFound, 404));
        var controller = Controller(Response<QuotaMutationDto>.Fail(QuotaErrorCodes.LimitExceeded, 409), quota.Object);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Consume(Request(), CancellationToken.None));

        var plain = Assert.IsType<Response<QuotaMutationDto>>(result.Value);
        Assert.Equal([QuotaErrorCodes.LimitExceeded], plain.Errors);
    }

    [Fact]
    public async Task A_status_read_that_throws_still_answers_the_refusal_never_a_500()
    {
        var quota = new Mock<IQuotaService>();
        quota.Setup(q => q.GetStatusResponseAsync(Tenant, QuotaKeys.UsersMax, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mongo down"));
        var controller = Controller(Response<QuotaMutationDto>.Fail(QuotaErrorCodes.LimitExceeded, 409), quota.Object);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Consume(Request(), CancellationToken.None));

        Assert.IsType<Response<QuotaMutationDto>>(result.Value);
    }

    private static TryConsumeQuotaRequest Request() =>
        new(Tenant, QuotaKeys.UsersMax, 1, "Diten.AuthService", Guid.NewGuid().ToString(), null, "test", null, null);

    private static InternalQuotasController Controller(Response<QuotaMutationDto> consumeAnswer, IQuotaService quota)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<TryConsumeQuotaCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(consumeAnswer);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuthService:InternalApiKey"] = Key })
            .Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Internal-Api-Key"] = Key;
        return new InternalQuotasController(mediator.Object, quota, configuration, NullLogger<InternalQuotasController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static QuotaStatusDto Status(decimal limit, decimal current) => new(
        Tenant, QuotaKeys.UsersMax, current, limit, limit == 0 ? 0 : current / limit * 100, false, current >= limit,
        DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(29), "Plan", null, null, null, false, false, null, null);
}
