using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Queries;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0262-FU01 DL-2 — the trusted document repository consumer gate (§8.1). Emsal
/// <c>TrustedSourceAuditIntentRequestExecutor</c> (header pre-check, then identity) and
/// <c>TrustedWorkflowConsumerRequestExecutor</c> (service principal swapped in for the command, then restored).
/// <list type="number">
/// <item><b>Within a 2 s budget</b> (504 when exceeded; no record, warning log): <c>Content-Length</c> over the request
/// limit → 413; several <c>Authorization</c> values, <c>X-Tenant-Id</c> or <c>X-Delegated-Authorization</c> → 403; no exact
/// <c>Bearer</c> → 401; explicit <c>AuthenticateAsync</c> with the trusted document repository scheme (scheme not
/// configured → 503, no record; any failure, the claim contract included → 401); claim defence (→ 403, unit-tested only);
/// closed list (empty → 503, no record; client not listed → 403 under the token's tenant).</item>
/// <item><b>Outside the budget</b>, under the token's tenant (<c>TenantScope</c>) and with the service principal as
/// <c>HttpContext.User</c>: multipart fields (≤ 16 KiB, before the file), scope, then the command or query with
/// <c>HttpContext.RequestAborted</c>.</item>
/// </list>
/// <b>One writer per outcome:</b> everything refused before a command is sent, a validator refusal, a query's 404 and a
/// client cancellation during a command are written by <see cref="TrustedDocumentRepositoryRefusalAudit"/>; every outcome
/// INSIDE a command is written by <c>AuditBehavior</c> alone — this class never looks at a command's answer to write.
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerRequestExecutor : ITrustedDocumentRepositoryConsumerRequestExecutor
{
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    public const string CorrelationHeader = "X-Correlation-Id";
    public const string StoreEndpoint = "POST api/internal/v1/document-repository/trusted-consumer/objects";
    public const string GetEndpoint = "GET api/internal/v1/document-repository/trusted-consumer/objects/{contentId}";
    public const string ContentEndpoint = "GET api/internal/v1/document-repository/trusted-consumer/objects/{contentId}/content";
    public const string CompensateEndpoint = "POST api/internal/v1/document-repository/trusted-consumer/objects/{contentId}/compensate";

    private const string OwningItemIdField = "owningItemId";
    private const string OwningVersionIdField = "owningVersionId";
    private const string CompanyIdField = "companyId";
    private const string ScopeField = "scope";
    private const string FileField = "file";

    private static readonly string[] RequiredFields = [OwningItemIdField, OwningVersionIdField, CompanyIdField, ScopeField];

    private static readonly string[] ForbiddenAuthorityHeaders =
    [
        "X-Tenant-Id",
        TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader
    ];

    private readonly ISender _sender;
    private readonly ITenantContext _tenantContext;
    private readonly IRepositoryObjectRepository _objects;
    private readonly TrustedDocumentRepositoryRefusalAudit _refusals;
    private readonly IOptions<TrustedDocumentRepositoryConsumerAuthorizationOptions> _consumers;
    private readonly ILogger<TrustedDocumentRepositoryConsumerRequestExecutor> _logger;

    public TrustedDocumentRepositoryConsumerRequestExecutor(
        ISender sender,
        ITenantContext tenantContext,
        IRepositoryObjectRepository objects,
        TrustedDocumentRepositoryRefusalAudit refusals,
        IOptions<TrustedDocumentRepositoryConsumerAuthorizationOptions> consumers,
        ILogger<TrustedDocumentRepositoryConsumerRequestExecutor> logger)
    {
        _sender = sender;
        _tenantContext = tenantContext;
        _objects = objects;
        _refusals = refusals;
        _consumers = consumers;
        _logger = logger;
    }

    public Task<IActionResult> StoreAsync(HttpContext httpContext) =>
        RunAsync(httpContext, StoreEndpoint, StoreWithinScopeAsync);

    public Task<IActionResult> GetAsync(HttpContext httpContext, Guid contentId) =>
        RunAsync(httpContext, GetEndpoint, call => GetWithinScopeAsync(call, contentId));

    public Task<IActionResult> OpenContentAsync(HttpContext httpContext, Guid contentId) =>
        RunAsync(httpContext, ContentEndpoint, call => OpenContentWithinScopeAsync(call, contentId));

    public Task<IActionResult> CompensateAsync(HttpContext httpContext, Guid contentId) =>
        RunAsync(httpContext, CompensateEndpoint, call => CompensateWithinScopeAsync(call, contentId));

    // ── The common frame: budgeted gate, refusal records, tenant scope and service principal around the body.

    private async Task<IActionResult> RunAsync(HttpContext http, string endpoint, Func<Call, Task<IActionResult>> body)
    {
        var aborted = http.RequestAborted;
        var correlation = ResolveCorrelation(http);

        Gate gate;
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(aborted))
        {
            budget.CancelAfter(Budget);
            try
            {
                budget.Token.ThrowIfCancellationRequested();
                gate = await GateAsync(http, budget.Token);
            }
            catch (OperationCanceledException) when (!aborted.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Trusted document repository gate on {Endpoint} exceeded its {Budget} budget; storage was not called.",
                    endpoint, Budget);
                return Failure(StatusCodes.Status504GatewayTimeout, DocumentRepositoryReasonCodes.TrustedConsumerTimeout, correlation);
            }
        }

        switch (gate.Kind)
        {
            case GateKind.Unavailable:
                _logger.LogWarning(
                    "Trusted document repository surface on {Endpoint} is unavailable (scheme not configured or no consumer listed).",
                    endpoint);
                return Failure(StatusCodes.Status503ServiceUnavailable, DocumentRepositoryReasonCodes.TrustedConsumerUnavailable, correlation);

            case GateKind.UnauthenticatedRefusal:
                await _refusals.RecordUnauthenticatedAsync(endpoint, gate.ReasonCode!, gate.Outcome, correlation, aborted);
                return Failure(gate.StatusCode, gate.ReasonCode!, correlation);

            case GateKind.AuthenticatedRefusal:
                using (TenantScope.Begin(_tenantContext, gate.Identity!.TenantId))
                {
                    await _refusals.RecordAuthenticatedAsync(
                        gate.Identity, endpoint, gate.ReasonCode!, gate.Outcome, null, correlation, aborted);
                }

                return Failure(gate.StatusCode, gate.ReasonCode!, correlation);
        }

        var previousPrincipal = http.User;
        try
        {
            http.User = gate.Principal!;
            using (TenantScope.Begin(_tenantContext, gate.Identity!.TenantId))
            {
                return await body(new Call(http, endpoint, gate.Identity, gate.Consumer!, correlation, aborted));
            }
        }
        catch (Exception ex) when (aborted.IsCancellationRequested && ex is OperationCanceledException or IOException)
        {
            // The client went away before a command was sent (reading the form fields): nothing ran, nothing to record.
            return new EmptyResult();
        }
        finally
        {
            http.User = previousPrincipal;
        }
    }

    private async Task<Gate> GateAsync(HttpContext http, CancellationToken budget)
    {
        var request = http.Request;
        if (request.ContentLength is > TrustedDocumentRepositoryUploadLimits.MaxRequestBytes)
        {
            return Gate.Unauthenticated(
                StatusCodes.Status413PayloadTooLarge, DocumentRepositoryReasonCodes.TrustedConsumerTooLarge, AuditOutcome.Failed);
        }

        if (request.Headers.Authorization.Count > 1
            || ForbiddenAuthorityHeaders.Any(request.Headers.ContainsKey))
        {
            return Gate.Unauthenticated(
                StatusCodes.Status403Forbidden, DocumentRepositoryReasonCodes.TrustedConsumerForbidden, AuditOutcome.Denied);
        }

        if (request.Headers.Authorization.Count != 1
            || !TrustedServiceTokenValidationExtensions.TryReadExactBearer(request.Headers.Authorization[0], out _))
        {
            return Unauthenticated401();
        }

        AuthenticateResult authentication;
        try
        {
            authentication = await http
                .AuthenticateAsync(TrustedServiceTokenValidationExtensions.DocumentRepositoryAuthenticationScheme)
                .WaitAsync(budget);
        }
        catch (InvalidOperationException)
        {
            return Gate.Unavailable;
        }

        if (!authentication.Succeeded || authentication.Principal?.Identity?.IsAuthenticated != true)
        {
            return Unauthenticated401();
        }

        // Defence in depth (r6, SB F-1): a subset of the scheme's own claim contract, so a token that passed the scheme
        // passes this too. Reachable only with a hand-built principal (unit test), never with the real scheme on a host.
        if (!TryResolveIdentity(authentication.Principal, out var identity))
        {
            return Gate.Unauthenticated(
                StatusCodes.Status403Forbidden, DocumentRepositoryReasonCodes.TrustedConsumerForbidden, AuditOutcome.Denied);
        }

        var entries = _consumers.Value.Entries;
        if (entries.Count == 0)
        {
            return Gate.Unavailable;
        }

        var entry = entries.SingleOrDefault(candidate => candidate.ClientId == identity.ClientId);
        if (entry is null)
        {
            return Gate.Authenticated(
                identity, StatusCodes.Status403Forbidden, DocumentRepositoryReasonCodes.TrustedConsumerForbidden, AuditOutcome.Denied);
        }

        return Gate.Pass(
            identity,
            authentication.Principal,
            new TrustedRepositoryConsumer(
                identity.ClientId, entry.ServiceName, entry.Audience, entry.ConsumerModule, entry.Scopes.ToArray()));
    }

    private static Gate Unauthenticated401() => Gate.Unauthenticated(
        StatusCodes.Status401Unauthorized, DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated, AuditOutcome.Denied);

    // ── Upload.

    private async Task<IActionResult> StoreWithinScopeAsync(Call call)
    {
        var request = call.Http.Request;
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !string.Equals(contentType.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(contentType.Boundary).Value is not { Length: > 0 } boundary)
        {
            return await RefuseAuthenticatedAsync(
                call, StatusCodes.Status400BadRequest, DocumentRepositoryReasonCodes.ValidationFailed, AuditOutcome.Failed, null);
        }

        var reader = new MultipartReader(boundary, request.Body)
        {
            HeadersLengthLimit = TrustedDocumentRepositoryUploadLimits.MaxFormFieldBytes,
            HeadersCountLimit = 8
        };

        UploadForm? form;
        try
        {
            form = await ReadFieldsAsync(reader, call.Aborted);
        }
        catch (Exception ex) when (!call.Aborted.IsCancellationRequested && ex is IOException or InvalidDataException)
        {
            form = null;
        }

        if (form is null)
        {
            return await RefuseAuthenticatedAsync(
                call, StatusCodes.Status400BadRequest, DocumentRepositoryReasonCodes.ValidationFailed, AuditOutcome.Failed, null);
        }

        // A known scope the entry does not allow (v1: anything but Documents) → 403 before the file section is read.
        if (!call.Consumer.Scopes.Contains(form.Scope.ToString(), StringComparer.Ordinal))
        {
            return await RefuseAuthenticatedAsync(
                call, StatusCodes.Status403Forbidden, DocumentRepositoryReasonCodes.TrustedConsumerForbidden, AuditOutcome.Denied, null);
        }

        var content = new TrustedUploadLimitedReadStream(
            form.File.Body,
            TrustedDocumentRepositoryUploadLimits.MaxFileBytes,
            TrustedUploadLimitedReadStream.TrailingSectionProbe(reader));
        var command = new StoreTrustedRepositoryObjectCommand(
            new StoreTrustedRepositoryObjectInput(
                form.Scope, form.CompanyId, form.OwningItemId, form.OwningVersionId, form.FileName, form.MediaType, content),
            call.Consumer,
            call.Correlation);

        return await SendCommandAsync(call, AuditOperation.Create, null, () => _sender.Send(command, call.Aborted));
    }

    /// <summary>
    /// Reads the four form fields (each exactly once, before the file, ≤ 16 KiB together, nothing unknown — a
    /// <c>tenantId</c> field included) and stops at the file section without reading it. Null = 400.
    /// </summary>
    private static async Task<UploadForm?> ReadFieldsAsync(MultipartReader reader, CancellationToken ct)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var fieldBytes = 0;

        while (await reader.ReadNextSectionAsync(ct) is { } section)
        {
            // IsFormDisposition() is true only for a NON-file part, so the disposition type is compared directly.
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
            if (disposition.IsFileDisposition())
            {
                var fileName = HeaderUtilities.RemoveQuotes(
                    disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value;
                if (!string.Equals(name, FileField, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(fileName)
                    || values.Count != RequiredFields.Length)
                {
                    return null;
                }

                return TryBuildForm(values, section, fileName);
            }

            if (name is null || !RequiredFields.Contains(name, StringComparer.Ordinal) || values.ContainsKey(name))
            {
                return null;
            }

            var value = await ReadBoundedAsync(section.Body, TrustedDocumentRepositoryUploadLimits.MaxFormFieldBytes - fieldBytes, ct);
            if (value is null)
            {
                return null;
            }

            fieldBytes += value.Length;
            values[name] = Encoding.UTF8.GetString(value);
        }

        return null; // no file section
    }

    private static UploadForm? TryBuildForm(Dictionary<string, string> values, MultipartSection file, string fileName)
    {
        if (!TryParseGuid(values[OwningItemIdField], out var owningItemId)
            || !TryParseGuid(values[OwningVersionIdField], out var owningVersionId)
            || !TryParseGuid(values[CompanyIdField], out var companyId)
            || !TryParseScope(values[ScopeField], out var scope))
        {
            return null;
        }

        return new UploadForm(owningItemId, owningVersionId, companyId, scope, fileName, file.ContentType, file);
    }

    /// <summary>Only the <c>D</c> form, never <see cref="Guid.Empty"/> (§8.1 "Sınır değerleri").</summary>
    private static bool TryParseGuid(string value, out Guid parsed) =>
        Guid.TryParseExact(value, "D", out parsed) && parsed != Guid.Empty;

    /// <summary>
    /// Only an enum NAME, case-sensitive, no surrounding space. <c>Enum.TryParse</c> is not used: it accepts numbers
    /// (<c>0</c>) and, with ignoreCase, <c>documents</c>.
    /// </summary>
    private static bool TryParseScope(string value, out ContentStorageScope scope)
    {
        foreach (var candidate in Enum.GetValues<ContentStorageScope>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.Ordinal))
            {
                scope = candidate;
                return true;
            }
        }

        scope = default;
        return false;
    }

    private static async Task<byte[]?> ReadBoundedAsync(Stream body, int remaining, CancellationToken ct)
    {
        if (remaining < 0)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        while (true)
        {
            var read = await body.ReadAsync(chunk.AsMemory(), ct);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > remaining)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }

    // ── Reads.

    private async Task<IActionResult> GetWithinScopeAsync(Call call, Guid contentId)
    {
        var response = await _sender.Send(
            new GetTrustedRepositoryObjectByIdQuery(contentId, call.Consumer, call.CorrelationText), call.Aborted);
        if (response.StatusCode == StatusCodes.Status404NotFound)
        {
            await RecordNotFoundAsync(call, contentId);
        }

        return ToResult(response);
    }

    private async Task<IActionResult> OpenContentWithinScopeAsync(Call call, Guid contentId)
    {
        var response = await _sender.Send(
            new OpenTrustedRepositoryObjectContentQuery(contentId, call.Consumer, call.CorrelationText, call.Correlation),
            call.Aborted);
        if (!response.IsSuccessful || response.Data is null)
        {
            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                await RecordNotFoundAsync(call, contentId);
            }

            return ToResult(response);
        }

        return new FileStreamResult(response.Data.Content, response.Data.MediaType)
        {
            FileDownloadName = response.Data.FileName
        };
    }

    // ── Compensation.

    private async Task<IActionResult> CompensateWithinScopeAsync(Call call, Guid contentId)
    {
        // r5 F-3 — the pre-read decides visibility (404 here, no command) and fixes what the record will say: whether the
        // row was already compensated when THIS call looked, and its scope.
        var row = await _objects.GetByContentIdIncludingCompensatedAsync(contentId, call.Aborted);
        if (row is null
            || !DocumentRepositoryAccessPolicy.IsVisibleToTrustedConsumer(row, call.Consumer.ConsumerModule, call.Consumer.Scopes))
        {
            await RecordNotFoundAsync(call, contentId);
            return Failure(StatusCodes.Status404NotFound, DocumentRepositoryReasonCodes.NotFoundNonLeakage, call.Correlation);
        }

        var command = new CompensateTrustedRepositoryObjectCommand(contentId, call.Consumer, call.Correlation)
        {
            AlreadyCompensated = row.IsDeleted,
            Scope = row.Scope
        };

        return await SendCommandAsync(call, AuditOperation.Delete, contentId, () => _sender.Send(command, call.Aborted));
    }

    // ── Shared.

    /// <summary>
    /// Sends a command. Its answer is returned as it is — <c>AuditBehavior</c> recorded it. Two outcomes never reach
    /// <c>AuditBehavior</c> and are recorded here: a validator refusal (<c>ValidationBehavior</c> runs first) and a client
    /// cancellation (<c>AuditBehavior</c> passes <see cref="OperationCanceledException"/> through unrecorded; §8.1 Dal 1).
    /// </summary>
    private async Task<IActionResult> SendCommandAsync<T>(
        Call call, AuditOperation operation, Guid? contentId, Func<Task<Response<T>>> send)
    {
        try
        {
            return ToResult(await send());
        }
        catch (FluentValidation.ValidationException)
        {
            return await RefuseAuthenticatedAsync(
                call, StatusCodes.Status400BadRequest, DocumentRepositoryReasonCodes.ValidationFailed, AuditOutcome.Failed, contentId);
        }
        catch (OperationCanceledException) when (call.Aborted.IsCancellationRequested)
        {
            await _refusals.RecordClientAbortedAsync(call.Identity, call.Endpoint, operation, contentId, call.Correlation);
            return new EmptyResult();
        }
    }

    private Task RecordNotFoundAsync(Call call, Guid contentId) =>
        _refusals.RecordAuthenticatedAsync(
            call.Identity, call.Endpoint, DocumentRepositoryReasonCodes.NotFoundNonLeakage, AuditOutcome.Denied,
            contentId, call.Correlation, call.Aborted);

    private async Task<IActionResult> RefuseAuthenticatedAsync(
        Call call, int statusCode, string reasonCode, AuditOutcome outcome, Guid? contentId)
    {
        await _refusals.RecordAuthenticatedAsync(
            call.Identity, call.Endpoint, reasonCode, outcome, contentId, call.Correlation, call.Aborted);
        return Failure(statusCode, reasonCode, call.Correlation);
    }

    private static bool TryResolveIdentity(ClaimsPrincipal? principal, out TrustedDocumentRepositoryConsumerIdentity identity)
    {
        identity = null!;
        if (principal?.Identity?.IsAuthenticated != true
            || !TryOneGuid(principal, JwtRegisteredClaimNames.Sub, out var clientId)
            || !TryOneGuid(principal, "tenant_id", out var tenantId)
            || !TryOneGuid(principal, JwtRegisteredClaimNames.Jti, out var tokenId)
            || !TryOneExact(principal, "actor_type", "service")
            || !TryOneExact(principal, "service_name", TrustedServiceTokenValidationExtensions.RequiredServiceName)
            || !TryOneExact(principal, JwtRegisteredClaimNames.Aud,
                TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience))
        {
            return false;
        }

        identity = new TrustedDocumentRepositoryConsumerIdentity(
            clientId,
            tenantId,
            tokenId,
            TrustedServiceTokenValidationExtensions.RequiredServiceName,
            TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience);
        return true;
    }

    private static bool TryOneGuid(ClaimsPrincipal principal, string type, out Guid value)
    {
        value = Guid.Empty;
        var claims = principal.Claims.Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal)).ToArray();
        return claims.Length == 1
            && Guid.TryParseExact(claims[0].Value, "D", out value)
            && value != Guid.Empty;
    }

    private static bool TryOneExact(ClaimsPrincipal principal, string type, string expected)
    {
        var claims = principal.Claims.Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal)).ToArray();
        return claims.Length == 1 && string.Equals(claims[0].Value, expected, StringComparison.Ordinal);
    }

    /// <summary><c>X-Correlation-Id</c> becomes the records' correlation (a Guid as it is, other text hashed); none → new.</summary>
    private static Guid ResolveCorrelation(HttpContext http)
    {
        var header = http.Request.Headers[CorrelationHeader];
        return AuditCorrelation.Resolve(header.Count == 1 ? header[0] : null, Guid.NewGuid());
    }

    private static IActionResult ToResult<T>(Response<T> response) =>
        response.StatusCode == StatusCodes.Status204NoContent
            ? new NoContentResult()
            : new ObjectResult(response) { StatusCode = response.StatusCode };

    private static IActionResult Failure(int statusCode, string reasonCode, Guid correlation) =>
        new ObjectResult(Response<object>.Fail(reasonCode, statusCode, reasonCode, correlation.ToString("D")))
        {
            StatusCode = statusCode
        };

    private sealed record Call(
        HttpContext Http,
        string Endpoint,
        TrustedDocumentRepositoryConsumerIdentity Identity,
        TrustedRepositoryConsumer Consumer,
        Guid Correlation,
        CancellationToken Aborted)
    {
        public string CorrelationText => Correlation.ToString("D");
    }

    private sealed record UploadForm(
        Guid OwningItemId,
        Guid OwningVersionId,
        Guid CompanyId,
        ContentStorageScope Scope,
        string FileName,
        string? MediaType,
        MultipartSection File);

    private enum GateKind
    {
        Pass,
        Unavailable,
        UnauthenticatedRefusal,
        AuthenticatedRefusal
    }

    private sealed record Gate(
        GateKind Kind,
        int StatusCode,
        string? ReasonCode,
        AuditOutcome Outcome,
        TrustedDocumentRepositoryConsumerIdentity? Identity,
        ClaimsPrincipal? Principal,
        TrustedRepositoryConsumer? Consumer)
    {
        public static Gate Unavailable { get; } =
            new(GateKind.Unavailable, StatusCodes.Status503ServiceUnavailable, null, AuditOutcome.Unknown, null, null, null);

        public static Gate Unauthenticated(int statusCode, string reasonCode, AuditOutcome outcome) =>
            new(GateKind.UnauthenticatedRefusal, statusCode, reasonCode, outcome, null, null, null);

        public static Gate Authenticated(
            TrustedDocumentRepositoryConsumerIdentity identity, int statusCode, string reasonCode, AuditOutcome outcome) =>
            new(GateKind.AuthenticatedRefusal, statusCode, reasonCode, outcome, identity, null, null);

        public static Gate Pass(
            TrustedDocumentRepositoryConsumerIdentity identity, ClaimsPrincipal principal, TrustedRepositoryConsumer consumer) =>
            new(GateKind.Pass, StatusCodes.Status200OK, null, AuditOutcome.Unknown, identity, principal, consumer);
    }
}

/// <summary>
/// MOD-0262-FU01 DL-2 — the one registration of the trusted document repository consumer surface, called by Program.cs
/// and by the HTTP test hosts: the closed list bound and validated at start-up (<c>ValidateOnStart</c>), the refusal
/// record writer with its process-wide limiter, the executor. The JWT scheme itself is part of
/// <c>AddTrustedServiceTokenValidation</c>.
/// </summary>
public static class TrustedDocumentRepositoryConsumerRegistration
{
    public static IServiceCollection AddTrustedDocumentRepositoryConsumer(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TrustedDocumentRepositoryConsumerAuthorizationOptions>()
            .Bind(configuration.GetSection(TrustedDocumentRepositoryConsumerAuthorizationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TrustedDocumentRepositoryConsumerAuthorizationOptions>,
            TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator>();
        services.AddSingleton<TrustedDocumentRepositoryRefusalAudit.UnauthenticatedLimiter>();
        services.AddScoped<TrustedDocumentRepositoryRefusalAudit>();
        services.AddScoped<ITrustedDocumentRepositoryConsumerRequestExecutor, TrustedDocumentRepositoryConsumerRequestExecutor>();
        return services;
    }
}
