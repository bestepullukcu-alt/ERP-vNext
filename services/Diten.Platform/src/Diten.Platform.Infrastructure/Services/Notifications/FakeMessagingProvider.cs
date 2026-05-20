using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.Notifications;

public sealed class FakeMessagingProvider : IMessagingProvider
{
    private readonly IOptions<FakeMessagingProviderOptions> _options;
    private readonly IHostEnvironment _environment;

    public FakeMessagingProvider(IOptions<FakeMessagingProviderOptions> options, IHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

    public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
    {
        if (!_options.Value.Enabled)
        {
            return Task.FromResult(MessagingProviderResult.Fail("FakeProviderDisabled", "Fake messaging provider is disabled."));
        }

        if (_environment.IsProduction())
        {
            return Task.FromResult(MessagingProviderResult.Fail("FakeProviderProductionBlocked", "Fake messaging provider is not allowed in production."));
        }

        return Task.FromResult(MessagingProviderResult.Success($"fake-{request.DispatchId:N}"));
    }
}

public sealed class MessagingProviderResolver : IMessagingProviderResolver
{
    private readonly IEnumerable<IMessagingProvider> _providers;

    public MessagingProviderResolver(IEnumerable<IMessagingProvider> providers)
    {
        _providers = providers;
    }

    public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode)
    {
        var provider = _providers.FirstOrDefault(x => x.ProviderCode == providerCode);
        return provider is null
            ? Response<IMessagingProvider>.Fail($"{providerCode} provider is not available in this runtime. Concrete providers are deferred to MOD-0263.", 400)
            : Response<IMessagingProvider>.Success(provider);
    }
}
