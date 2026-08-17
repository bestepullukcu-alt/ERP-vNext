using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class UpsertTenantMessagingSettingsHandler
    : IRequestHandler<UpsertTenantMessagingSettingsCommand, Response<TenantMessagingSettingsDto>>
{
    private readonly ITenantMessagingSettingsRepository _repository;

    public UpsertTenantMessagingSettingsHandler(ITenantMessagingSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<TenantMessagingSettingsDto>> Handle(UpsertTenantMessagingSettingsCommand request, CancellationToken ct)
    {
        if (!NotificationParsing.TryParseProvider(request.Request.ProviderCode, out var provider))
        {
            return Response<TenantMessagingSettingsDto>.Fail("ProviderCode is invalid.", 400);
        }

        if (!NotificationParsing.TryParseFallbackPolicy(request.Request.FallbackPolicy, out var fallbackPolicy))
        {
            return Response<TenantMessagingSettingsDto>.Fail("FallbackPolicy is invalid.", 400);
        }

        var existing = await _repository.GetByTenantIdAsync(request.TenantId, ct);
        var settings = existing ?? new TenantMessagingSettings
        {
            TenantId = request.TenantId,
            IsPlatformDefault = false
        };

        settings.ProviderCode = provider;
        settings.SenderEmail = NotificationParsing.NormalizeEmail(request.Request.SenderEmail);
        settings.SenderName = NormalizeNullable(request.Request.SenderName);
        settings.ReplyToEmail = NormalizeNullableEmail(request.Request.ReplyToEmail);
        settings.Host = NormalizeNullable(request.Request.Host);
        settings.Port = request.Request.Port;
        settings.UseSsl = request.Request.UseSsl;
        settings.ApiBaseUrl = NormalizeNullable(request.Request.ApiBaseUrl);
        settings.CredentialSecretRef = NormalizeNullable(request.Request.CredentialSecretRef);
        settings.IsEnabled = request.Request.IsEnabled;
        settings.FallbackPolicy = fallbackPolicy;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            await _repository.CreateAsync(settings, ct);
            return Response<TenantMessagingSettingsDto>.Success(settings.ToDto(), 201);
        }

        await _repository.UpdateAsync(settings, ct);
        return Response<TenantMessagingSettingsDto>.Success(settings.ToDto());
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeNullableEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NotificationParsing.NormalizeEmail(value);
}
