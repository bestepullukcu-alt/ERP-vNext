using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;

public static class ExternalContextReferenceContract
{
    public const string Name = "ppm.external-context-reference";
    public const string Version = "1.0";

    public static string? PermissionFor(string contextKind) => contextKind switch
    {
        "Portfolio" => PpmPermissions.PortfoliosRead,
        "Initiative" => PpmPermissions.InitiativesRead,
        "Program" => PpmPermissions.ProgramsRead,
        "Project" => PpmPermissions.ProjectsRead,
        _ => null
    };
}

public sealed class ValidateExternalContextReferenceRequest
{
    [JsonPropertyName("contractName")]
    public string? ContractName { get; init; }

    [JsonPropertyName("contractVersion")]
    public string? ContractVersion { get; init; }

    [JsonPropertyName("contextKind")]
    public string? ContextKind { get; init; }

    [JsonPropertyName("contextId")]
    public string? ContextId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record ExternalContextReferenceResponse(
    [property: JsonPropertyName("contractName")] string ContractName,
    [property: JsonPropertyName("contractVersion")] string ContractVersion,
    [property: JsonPropertyName("contextKind")] string ContextKind,
    [property: JsonPropertyName("contextId")] string ContextId);

public sealed record ValidateExternalContextReferenceQuery(
    string? ContractName,
    string? ContractVersion,
    string? ContextKind,
    string? ContextId,
    bool HasAdditionalProperties)
    : IRequest<Response<ExternalContextReferenceResponse>>;

public sealed class ValidateExternalContextReferenceValidator
    : AbstractValidator<ValidateExternalContextReferenceQuery>
{
    public ValidateExternalContextReferenceValidator()
    {
        RuleFor(x => x.ContractName).Equal(ExternalContextReferenceContract.Name);
        RuleFor(x => x.ContractVersion).Equal(ExternalContextReferenceContract.Version);
        RuleFor(x => x.ContextKind)
            .Must(value => value is not null && ExternalContextReferenceContract.PermissionFor(value) is not null)
            .WithMessage("ContextKind is invalid.");
        RuleFor(x => x.ContextId)
            .Must(IsCanonicalNonEmptyGuid)
            .WithMessage("ContextId must be a canonical non-empty Guid.");
        RuleFor(x => x.HasAdditionalProperties).Equal(false)
            .WithMessage("Unknown contract fields are not allowed.");
    }

    private static bool IsCanonicalNonEmptyGuid(string? value) =>
        value is not null &&
        Guid.TryParseExact(value, "D", out var parsed) &&
        parsed != Guid.Empty &&
        string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal);
}

public sealed record ExternalContextReferenceLookupResult(
    bool IsReferenceable,
    string? VisibilityPolicyKey);

public interface IExternalContextReferenceLookup
{
    Task<ExternalContextReferenceLookupResult?> FindAsync(
        Guid tenantId,
        string contextKind,
        Guid contextId,
        CancellationToken cancellationToken);
}

public interface IExternalContextReferenceLookupTimeout
{
    TimeSpan LookupTimeout { get; }
}

public sealed class ExternalContextReferenceDependencyException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class ValidateExternalContextReferenceHandler(
    ITenantContext tenant,
    IPpmAccessAuthorizer authorizer,
    IExternalContextReferenceLookup lookup,
    IExternalContextReferenceLookupTimeout lookupTimeout)
    : IRequestHandler<ValidateExternalContextReferenceQuery, Response<ExternalContextReferenceResponse>>
{
    public async Task<Response<ExternalContextReferenceResponse>> Handle(
        ValidateExternalContextReferenceQuery request,
        CancellationToken cancellationToken)
    {
        var permission = ExternalContextReferenceContract.PermissionFor(request.ContextKind!);
        if (permission is null ||
            !Guid.TryParseExact(request.ContextId, "D", out var contextId) ||
            contextId == Guid.Empty)
        {
            return Response<ExternalContextReferenceResponse>.Fail("Invalid external context reference.", 400);
        }

        var access = await authorizer.AuthorizeAsync(permission, cancellationToken);
        if (access != PpmAccessDecision.Allowed)
        {
            return access.Failure<ExternalContextReferenceResponse>();
        }

        using var lookupCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lookupCancellation.CancelAfter(lookupTimeout.LookupTimeout);

        try
        {
            var candidate = await lookup.FindAsync(
                tenant.TenantId,
                request.ContextKind!,
                contextId,
                lookupCancellation.Token);

            if (candidate is null || !candidate.IsReferenceable || candidate.VisibilityPolicyKey is not null)
            {
                return Response<ExternalContextReferenceResponse>.Fail("External context was not found.", 404);
            }

            return Response<ExternalContextReferenceResponse>.Success(new(
                ExternalContextReferenceContract.Name,
                ExternalContextReferenceContract.Version,
                request.ContextKind!,
                contextId.ToString("D")));
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested &&
                  lookupCancellation.IsCancellationRequested)
        {
            return Response<ExternalContextReferenceResponse>.Fail(
                "Authoritative external context lookup is unavailable.",
                503);
        }
        catch (ExternalContextReferenceDependencyException)
        {
            return Response<ExternalContextReferenceResponse>.Fail(
                "Authoritative external context lookup is unavailable.",
                503);
        }
    }
}
