using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Contracts.Workflow;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.Workflow;

public sealed class PlatformProductIdentityWorkflowClient : IProductIdentityWorkflowClient
{
    public const string DelegatedAuthorizationHeader = "X-Delegated-Authorization";
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string StartPath = "/api/internal/v1/workflow/trusted-consumer/start";
    private const string StartResultPath = "/api/internal/v1/workflow/trusted-consumer/start-result";
    private const string EvidencePath = "/api/internal/v1/workflow/trusted-consumer/terminal-decision-evidence";
    private const string CancellationPreflightPath = "/api/internal/v1/workflow/trusted-consumer/cancel-preflight";
    private const string CancellationPath = "/api/internal/v1/workflow/trusted-consumer/cancel";
    private const int MaximumResponseBytes = 64 * 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private readonly IHttpClientFactory _clients;
    private readonly IProductIdentityWorkflowServiceIdentityProvider _identities;
    private readonly ProductIdentityWorkflowClientOptions _options;

    public PlatformProductIdentityWorkflowClient(
        IHttpClientFactory clients,
        IProductIdentityWorkflowServiceIdentityProvider identities,
        IOptions<ProductIdentityWorkflowClientOptions> options)
    {
        _clients = clients;
        _identities = identities;
        _options = options.Value;
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
        Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken, CancellationToken cancellationToken = default)
    {
        if (!ValidStartRequest(request)) return InvalidRequest<ProductIdentityWorkflowStartResult>();
        return SendAsync<ProductIdentityWorkflowStartResult>(tenantId, StartPath,
            new
            {
                request.TemplateId,
                request.TemplateCode,
                request.ObjectType,
                request.ObjectId,
                request.ObjectRef,
                request.CandidatePrincipalIds,
                request.ReasonCode,
                request.CommentRequired,
                request.EvidenceRequired,
                request.DueAt
            },
            request.IdempotencyKey, delegatedUserToken, value => ValidStartResult(value, request),
            allowCreated: true, callerToken: cancellationToken);
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
        Guid tenantId, ProductIdentityWorkflowStartResultRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidObject(request.ExpectedObjectType, request.ExpectedObjectId) || request.ExpectedMakerSubjectId == Guid.Empty)
            return InvalidRequest<ProductIdentityWorkflowStartResult>();
        return SendAsync<ProductIdentityWorkflowStartResult>(tenantId, StartResultPath,
            new { request.ExpectedObjectType, request.ExpectedObjectId, request.ExpectedMakerSubjectId },
            request.IdempotencyKey, null, value => ValidStartResult(value, null),
            allowCreated: false, callerToken: cancellationToken);
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
        Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (request.WorkflowInstanceId == Guid.Empty || !ValidObject(request.ExpectedObjectType, request.ExpectedObjectId))
            return InvalidRequest<ProductIdentityWorkflowTerminalEvidence>();
        return SendAsync<ProductIdentityWorkflowTerminalEvidence>(tenantId, EvidencePath, request, null, null,
            value => ValidEvidence(value, request),
            allowCreated: false, callerToken: cancellationToken);
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>>
        GetCancellationPreflightAsync(
            Guid tenantId,
            ProductIdentityWorkflowCancellationPreflightRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!ValidCancellationGraph(
                request.WorkflowInstanceId,
                request.ApprovalTaskId,
                request.ExpectedObjectType,
                request.ExpectedObjectId,
                request.ExpectedMakerSubjectId))
        {
            return InvalidRequest<ProductIdentityWorkflowCancellationPreflight>();
        }

        return SendAsync<ProductIdentityWorkflowCancellationPreflight>(
            tenantId,
            CancellationPreflightPath,
            request,
            null,
            null,
            value => ValidCancellationPreflight(value, request),
            allowCreated: false,
            callerToken: cancellationToken);
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>> CancelAsync(
        Guid tenantId,
        ProductIdentityWorkflowCancellationRequest request,
        string delegatedUserToken,
        CancellationToken cancellationToken = default)
    {
        if (!ValidCancellationGraph(
                request.WorkflowInstanceId,
                request.ApprovalTaskId,
                request.ExpectedObjectType,
                request.ExpectedObjectId,
                request.ExpectedMakerSubjectId)
            || request.ExpectedWorkflowInstanceVersion <= 0
            || request.ExpectedApprovalTaskVersion <= 0
            || !ValidText(request.ReasonCode, 128)
            || !ValidOptionalText(request.Comment, 2000))
        {
            return InvalidRequest<ProductIdentityWorkflowCancellationEvidence>();
        }

        return SendAsync<ProductIdentityWorkflowCancellationEvidence>(
            tenantId,
            CancellationPath,
            new
            {
                request.WorkflowInstanceId,
                request.ApprovalTaskId,
                request.ExpectedObjectType,
                request.ExpectedObjectId,
                request.ExpectedMakerSubjectId,
                request.ExpectedWorkflowInstanceVersion,
                request.ExpectedApprovalTaskVersion,
                request.ReasonCode,
                request.Comment
            },
            request.IdempotencyKey,
            delegatedUserToken,
            value => ValidCancellationEvidence(value, request),
            allowCreated: false,
            callerToken: cancellationToken);
    }

    private async Task<ProductIdentityWorkflowTransportResult<T>> SendAsync<T>(
        Guid tenantId, string path, object body, string? idempotencyKey, string? delegatedToken,
        Func<T, bool> validateSuccess, bool allowCreated, CancellationToken callerToken)
    {
        var requiresIdempotency = path is StartPath or StartResultPath or CancellationPath;
        var requiresDelegatedToken = path is StartPath or CancellationPath;
        if (tenantId == Guid.Empty || !ValidConfiguration()
            || !ValidHeader(idempotencyKey, 256, allowNull: !requiresIdempotency)
            || !ValidHeader(delegatedToken, 16 * 1024, allowNull: !requiresDelegatedToken))
            return ProductIdentityWorkflowTransportResult<T>.Fail(ProductIdentityWorkflowTransportOutcome.Invalid, "PRODUCT_WORKFLOW_REQUEST_INVALID");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        budget.CancelAfter(Budget);
        try
        {
            var identity = await _identities.GetAsync(tenantId, false, budget.Token);
            var result = await SendOnceAsync(path, body, idempotencyKey, delegatedToken, identity, validateSuccess, allowCreated, budget.Token);
            if (result.Outcome != ProductIdentityWorkflowTransportOutcome.AuthenticationRejected) return result;
            identity = await _identities.GetAsync(tenantId, true, budget.Token);
            return await SendOnceAsync(path, body, idempotencyKey, delegatedToken, identity, validateSuccess, allowCreated, budget.Token);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            return ProductIdentityWorkflowTransportResult<T>.Fail(ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        }
        catch (ProductIdentityWorkflowServiceIdentityException exception)
        {
            return ProductIdentityWorkflowTransportResult<T>.Fail(
                exception.IsRetryable ? ProductIdentityWorkflowTransportOutcome.Retryable : ProductIdentityWorkflowTransportOutcome.AuthenticationRejected,
                exception.ErrorCode);
        }
        catch (HttpRequestException)
        {
            return ProductIdentityWorkflowTransportResult<T>.Fail(ProductIdentityWorkflowTransportOutcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE");
        }
        catch (InvalidDataException)
        {
            return ProductIdentityWorkflowTransportResult<T>.Fail(ProductIdentityWorkflowTransportOutcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID");
        }
        catch (IOException)
        {
            return ProductIdentityWorkflowTransportResult<T>.Fail(ProductIdentityWorkflowTransportOutcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE");
        }
    }

    private async Task<ProductIdentityWorkflowTransportResult<T>> SendOnceAsync<T>(
        string path, object body, string? idempotencyKey, string? delegatedToken,
        ProductIdentityWorkflowServiceIdentity identity, Func<T, bool> validateSuccess,
        bool allowCreated, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.PlatformBaseUrl), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.AccessToken);
        if (delegatedToken is not null) request.Headers.TryAddWithoutValidation(DelegatedAuthorizationHeader, "Bearer " + delegatedToken);
        if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader, idempotencyKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _clients.CreateClient(nameof(PlatformProductIdentityWorkflowClient))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return Contract<T>();
        var payload = await ReadBoundedAsync(response.Content, cancellationToken);
        return Parse(response.StatusCode, payload, validateSuccess, allowCreated);
    }

    private static ProductIdentityWorkflowTransportResult<T> Parse<T>(
        HttpStatusCode status, byte[] payload, Func<T, bool> validateSuccess, bool allowCreated)
    {
        try
        {
            using var document = JsonDocument.Parse(payload, StrictOptions);
            var root = document.RootElement;
            RequireEnvelope(root);
            if (root.GetProperty("statusCode").GetInt32() != (int)status) return Contract<T>();
            var successful = root.GetProperty("isSuccessful").GetBoolean();
            if (status == HttpStatusCode.OK || allowCreated && status == HttpStatusCode.Created)
            {
                if (!successful || root.GetProperty("errors").GetArrayLength() != 0 || root.GetProperty("data").ValueKind != JsonValueKind.Object)
                    return Contract<T>();
                var value = JsonSerializer.Deserialize<T>(root.GetProperty("data").GetRawText(), JsonOptions);
                return value is null || !validateSuccess(value)
                    ? Contract<T>()
                    : ProductIdentityWorkflowTransportResult<T>.Success(value);
            }
            if (successful || root.GetProperty("errors").GetArrayLength() == 0) return Contract<T>();
            var reason = root.GetProperty("reason_code").ValueKind == JsonValueKind.String
                ? root.GetProperty("reason_code").GetString() : null;
            return MapFailure<T>(status, reason);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return Contract<T>();
        }
    }

    private static ProductIdentityWorkflowTransportResult<T> MapFailure<T>(HttpStatusCode status, string? code)
    {
        var error = string.IsNullOrWhiteSpace(code) ? "PRODUCT_WORKFLOW_RESPONSE_INVALID" : code;
        var outcome = status switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge => ProductIdentityWorkflowTransportOutcome.Invalid,
            HttpStatusCode.Unauthorized => ProductIdentityWorkflowTransportOutcome.AuthenticationRejected,
            HttpStatusCode.Forbidden => ProductIdentityWorkflowTransportOutcome.Forbidden,
            HttpStatusCode.NotFound => ProductIdentityWorkflowTransportOutcome.NotFound,
            HttpStatusCode.Conflict when code == "WORKFLOW_START_NOT_COMPLETED" => ProductIdentityWorkflowTransportOutcome.Incomplete,
            HttpStatusCode.Conflict when code == "WORKFLOW_DECISION_NOT_TERMINAL" => ProductIdentityWorkflowTransportOutcome.NonTerminal,
            HttpStatusCode.Conflict => ProductIdentityWorkflowTransportOutcome.Conflict,
            HttpStatusCode.GatewayTimeout => ProductIdentityWorkflowTransportOutcome.Timeout,
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => ProductIdentityWorkflowTransportOutcome.Retryable,
            _ when (int)status >= 500 => ProductIdentityWorkflowTransportOutcome.Retryable,
            _ => ProductIdentityWorkflowTransportOutcome.Invalid
        };
        return ProductIdentityWorkflowTransportResult<T>.Fail(outcome, error);
    }

    private bool ValidConfiguration() => Uri.TryCreate(_options.PlatformBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    private static bool ValidHeader(string? value, int max, bool allowNull) => value is null ? allowNull
        : value.Length is > 0 && value.Length <= max && string.Equals(value, value.Trim(), StringComparison.Ordinal)
          && !value.Any(char.IsControl) && !value.Contains(',');
    private static bool ValidStartRequest(ProductIdentityWorkflowStartRequest value) =>
        (value.TemplateId.HasValue ^ value.TemplateCode is not null)
        && (!value.TemplateId.HasValue || value.TemplateId.Value != Guid.Empty)
        && (value.TemplateCode is null || ValidText(value.TemplateCode, 128))
        && ValidObject(value.ObjectType, value.ObjectId) && ValidOptionalText(value.ObjectRef, 256)
        && value.CandidatePrincipalIds.Count is >= 1 and <= 100
        && value.CandidatePrincipalIds.All(x => Guid.TryParseExact(x, "D", out var id) && id != Guid.Empty)
        && value.CandidatePrincipalIds.Distinct(StringComparer.Ordinal).Count() == value.CandidatePrincipalIds.Count
        && ValidOptionalText(value.ReasonCode, 128) && ValidUtc(value.DueAt);
    private static bool ValidObject(string objectType, string objectId) =>
        objectType is "GlobalProduct" or "GlobalProductCorrection" or "GlobalProductRetirement"
            or "gsku" or "GskuCorrection" or "GskuRetirementRequest" or "lsku" or "LskuRetirementRequest" or "finished-good"
        && Guid.TryParseExact(objectId, "D", out var id) && id != Guid.Empty;
    private static bool ValidStartResult(
        ProductIdentityWorkflowStartResult value,
        ProductIdentityWorkflowStartRequest? request) =>
        value.WorkflowInstanceId != Guid.Empty && value.TemplateId != Guid.Empty && value.TemplateVersionId != Guid.Empty
        && value.ApprovalTaskId != Guid.Empty && value.AssignmentSnapshotId != Guid.Empty && value.StartTransitionLogId != Guid.Empty
        && ValidText(value.ObjectRef, 256) && ValidText(value.Status, 64) && ValidText(value.CurrentStage, 128)
        && ValidText(value.CurrentStep, 128) && value.StartedAt is { } startedAt
        && startedAt != default && startedAt.Offset == TimeSpan.Zero && ValidUtc(value.DueAt)
        && ValidOptionalText(value.CorrelationId, 256)
        && (request is null || (request.TemplateId is null || value.TemplateId == request.TemplateId)
            && (request.ObjectRef is null || string.Equals(value.ObjectRef, request.ObjectRef, StringComparison.Ordinal)));
    private static bool ValidEvidence(
        ProductIdentityWorkflowTerminalEvidence value,
        ProductIdentityWorkflowTerminalEvidenceRequest request) =>
        value.WorkflowInstanceId == request.WorkflowInstanceId && value.ApprovalTaskId != Guid.Empty
        && value.TemplateId != Guid.Empty && value.TemplateVersionId != Guid.Empty
        && string.Equals(value.ObjectType, request.ExpectedObjectType, StringComparison.Ordinal)
        && string.Equals(value.ObjectId, request.ExpectedObjectId, StringComparison.Ordinal)
        && ValidText(value.ObjectRef, 256) && value.TerminalAction is "Approve" or "Reject"
        && Guid.TryParseExact(value.ActorUserId, "D", out var actor) && actor != Guid.Empty
        && ValidOptionalText(value.ReasonCode, 256) && value.DecisionAt != default && value.DecisionAt.Offset == TimeSpan.Zero
        && value.TransitionSequence > 0 && ValidTerminalStatuses(
            value.TerminalAction, value.TaskStatus, value.InstanceStatus)
        && ValidOptionalText(value.CorrelationId, 256);
    private static bool ValidCancellationGraph(
        Guid workflowInstanceId,
        Guid approvalTaskId,
        string objectType,
        string objectId,
        Guid makerSubjectId) =>
        workflowInstanceId != Guid.Empty
        && approvalTaskId != Guid.Empty
        && makerSubjectId != Guid.Empty
        && ValidObject(objectType, objectId);
    private static bool ValidCancellationPreflight(
        ProductIdentityWorkflowCancellationPreflight value,
        ProductIdentityWorkflowCancellationPreflightRequest request) =>
        value.WorkflowInstanceId == request.WorkflowInstanceId
        && value.ApprovalTaskId == request.ApprovalTaskId
        && string.Equals(value.ObjectType, request.ExpectedObjectType, StringComparison.Ordinal)
        && string.Equals(value.ObjectId, request.ExpectedObjectId, StringComparison.Ordinal)
        && ValidText(value.ObjectRef, 256)
        && value.WorkflowInstanceVersion > 0
        && value.ApprovalTaskVersion > 0
        && string.Equals(value.WorkflowInstanceStatus, "Active", StringComparison.Ordinal)
        && value.ApprovalTaskStatus is "WaitingApproval" or "WaitingEvidence";
    private static bool ValidCancellationEvidence(
        ProductIdentityWorkflowCancellationEvidence value,
        ProductIdentityWorkflowCancellationRequest request) =>
        value.WorkflowInstanceId == request.WorkflowInstanceId
        && value.ApprovalTaskId == request.ApprovalTaskId
        && value.TemplateId != Guid.Empty
        && value.TemplateVersionId != Guid.Empty
        && string.Equals(value.ObjectType, request.ExpectedObjectType, StringComparison.Ordinal)
        && string.Equals(value.ObjectId, request.ExpectedObjectId, StringComparison.Ordinal)
        && ValidText(value.ObjectRef, 256)
        && string.Equals(value.TerminalAction, "Cancel", StringComparison.Ordinal)
        && value.ActorUserId == request.ExpectedMakerSubjectId
        && string.Equals(value.ReasonCode, request.ReasonCode, StringComparison.Ordinal)
        && string.Equals(value.Comment, request.Comment, StringComparison.Ordinal)
        && value.DecisionAt != default
        && value.DecisionAt.Offset == TimeSpan.Zero
        && value.TransitionSequence > 0
        && value.TransitionLogId != Guid.Empty
        && string.Equals(value.TaskStatus, "Cancelled", StringComparison.Ordinal)
        && string.Equals(value.InstanceStatus, "Cancelled", StringComparison.Ordinal)
        && value.WorkflowInstanceVersion > request.ExpectedWorkflowInstanceVersion
        && value.ApprovalTaskVersion > request.ExpectedApprovalTaskVersion
        && ValidOptionalText(value.CorrelationId, 256);
    private static bool ValidText(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
    private static bool ValidOptionalText(string? value, int max) => value is null || ValidText(value, max);
    private static bool ValidTerminalStatuses(string action, string taskStatus, string instanceStatus) =>
        action switch
        {
            "Approve" => string.Equals(taskStatus, "Approved", StringComparison.Ordinal)
                && string.Equals(instanceStatus, "Completed", StringComparison.Ordinal),
            "Reject" => string.Equals(taskStatus, "Rejected", StringComparison.Ordinal)
                && string.Equals(instanceStatus, "Rejected", StringComparison.Ordinal),
            _ => false
        };
    private static bool ValidUtc(DateTimeOffset? value) => value is null || value.Value.Offset == TimeSpan.Zero;
    private static Task<ProductIdentityWorkflowTransportResult<T>> InvalidRequest<T>() => Task.FromResult(
        ProductIdentityWorkflowTransportResult<T>.Fail(ProductIdentityWorkflowTransportOutcome.Invalid, "PRODUCT_WORKFLOW_REQUEST_INVALID"));
    private static void RequireEnvelope(JsonElement root)
    {
        var expected = new HashSet<string>(["data", "statusCode", "isSuccessful", "errors", "reason_code", "correlation_id"], StringComparer.Ordinal);
        var found = root.ValueKind == JsonValueKind.Object ? root.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal) : [];
        if (!found.SetEquals(expected) || root.GetProperty("statusCode").ValueKind != JsonValueKind.Number
            || root.GetProperty("isSuccessful").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || root.GetProperty("errors").ValueKind != JsonValueKind.Array) throw new JsonException();
    }
    private static ProductIdentityWorkflowTransportResult<T> Contract<T>() =>
        ProductIdentityWorkflowTransportResult<T>.Fail(ProductIdentityWorkflowTransportOutcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID");
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes) throw new InvalidDataException();
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) return output.ToArray();
            if (output.Length + read > MaximumResponseBytes) throw new InvalidDataException();
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly JsonDocumentOptions StrictOptions = new() { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 };
}
