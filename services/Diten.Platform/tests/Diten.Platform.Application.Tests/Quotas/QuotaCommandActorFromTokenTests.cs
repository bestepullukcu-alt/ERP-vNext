using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Quotas.Services;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Quotas;

/// <summary>
/// BL-521 FIX1 F5 — the quota initialize / sync commands write their actor as the CALLER of the request (its token, via
/// <see cref="ICurrentUserContext"/>), never the <c>ActorId</c> a caller puts in the body: otherwise the stored "created
/// by" — the record of who did it — could be written in anybody's name, by a platform administrator as much as by a
/// partner. Measured on the production handlers.
/// </summary>
public sealed class QuotaCommandActorFromTokenTests
{
    private static readonly Guid Tenant = Guid.Parse("52152152-0000-4000-8000-00000000f501");
    private const string Caller = "caller.from.token@bl521.test";
    private const string Spoofed = "someone.else@bl521.test";

    [Fact]
    public async Task Quota_initialization_names_the_caller_of_the_request_not_the_actor_in_the_body()
    {
        var quotas = Quotas();

        await new InitializeTenantQuotasCommandHandler(quotas.Object, new Caller_()).Handle(
            new InitializeTenantQuotasCommand(Tenant, new InitializeTenantQuotasRequest("x", null, Spoofed, null)), CancellationToken.None);

        quotas.Verify(q => q.InitializeTenantQuotasAsync(Tenant, It.IsAny<string>(), It.IsAny<string>(), Caller, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        quotas.Verify(q => q.InitializeTenantQuotasAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), Spoofed, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Quota_limit_sync_names_the_caller_of_the_request_not_the_actor_in_the_body()
    {
        var quotas = Quotas();

        await new SyncTenantQuotaLimitsFromSubscriptionCommandHandler(quotas.Object, new Caller_()).Handle(
            new SyncTenantQuotaLimitsFromSubscriptionCommand(Tenant, new SyncTenantQuotaLimitsRequest("x", null, Spoofed, null)), CancellationToken.None);

        quotas.Verify(q => q.SyncTenantQuotaLimitsAsync(Tenant, It.IsAny<string>(), It.IsAny<string>(), Caller, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        quotas.Verify(q => q.SyncTenantQuotaLimitsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), Spoofed, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IQuotaService> Quotas()
    {
        var quotas = new Mock<IQuotaService>();
        quotas.Setup(q => q.InitializeTenantQuotasAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<QuotaStatusDto>>.Success([]));
        quotas.Setup(q => q.SyncTenantQuotaLimitsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<QuotaStatusDto>>.Success([]));
        return quotas;
    }

    /// <summary>The caller as the token names it.</summary>
    private sealed class Caller_ : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("52152152-0000-4000-8000-00000000f502");
        public string? Email => Caller;
        public string? DisplayName => "Caller";
        public string ActorName => Caller;
        public bool IsAuthenticated => true;
    }
}
