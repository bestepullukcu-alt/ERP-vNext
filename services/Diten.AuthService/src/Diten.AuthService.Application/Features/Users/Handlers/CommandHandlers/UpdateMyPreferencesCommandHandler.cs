using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

/// <summary>
/// WP-EMAIL-LANG-01 — a user sets (or clears) the language their mail is written in. Only their own record: the id and
/// tenant come from the token. Written as a field change (never a whole-document write), audited as a user update.
/// </summary>
public sealed class UpdateMyPreferencesCommandHandler(IUserRepository users, IUserAuditRecorder audit)
    : IRequestHandler<UpdateMyPreferencesCommand, Response<MyPreferencesDto>>
{
    public const string FieldName = "preferredLanguage";

    /// <summary>The preferred language is not one of the seven supported languages. The profile screen's code (delivery 2
    /// maps it), not the Users screen's: it is deliberately NOT in <see cref="UserErrorCodes"/>, whose every code the
    /// Users screen must show (UserErrorCodeBridgeGuardTests).</summary>
    public const string PreferredLanguageInvalidCode = "USER_PREFERRED_LANGUAGE_INVALID";

    public async Task<Response<MyPreferencesDto>> Handle(UpdateMyPreferencesCommand request, CancellationToken ct)
    {
        if (!UserLanguages.TryNormalize(request.PreferredLanguage, out var language))
        {
            return Response<MyPreferencesDto>.Fail(
                "PreferredLanguage must be one of: " + string.Join(", ", UserLanguages.Supported) + ".",
                [new ResponseError(PreferredLanguageInvalidCode)],
                400);
        }

        var user = await users.GetByIdAndTenantAsync(request.UserId, request.TenantId, ct);
        if (user is null)
        {
            return UserErrorCodes.NotFoundRefusal<MyPreferencesDto>();
        }

        var previous = user.PreferredLanguage;
        if (string.Equals(previous, language, StringComparison.Ordinal))
        {
            return Response<MyPreferencesDto>.Success(new MyPreferencesDto(previous)); // no change, no row
        }

        var state = users.CaptureState(user);
        user.SetPreferredLanguage(language);
        // FIX1 — the write's answer counts: false = the account is no longer there to write (deleted meanwhile). Nothing
        // changed, so nothing is audited.
        if (!await users.TryWriteChangesAsync(user, state, request.TenantId, UserWriteCondition.None, ct))
        {
            return UserErrorCodes.NotFoundRefusal<MyPreferencesDto>();
        }

        // BL-456 / AUD-001 — the field NAME, and its before/after: a language code is not personal data.
        await audit.RecordAsync(UserAuditEvents.Updated, request.TenantId, user.Id,
            new Dictionary<string, object?>
            {
                ["changedFields"] = new[] { FieldName },
                ["previousPreferredLanguage"] = previous,
                ["preferredLanguage"] = language,
                ["via"] = "self",
                ["correlationId"] = request.CorrelationId
            }, ct);

        return Response<MyPreferencesDto>.Success(new MyPreferencesDto(language));
    }
}
