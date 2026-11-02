using Diten.Platform.Application.Features.RecordCodes;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

public interface ITrustedRecordCodeRequestExecutor
{
    Task<IActionResult> ExecuteAsync(
        HttpContext context,
        CancellationToken cancellationToken,
        Func<TrustedRecordCodeCaller, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure);
}

/// <summary>
/// Who is asking and what, as the executor established it: the token's tenant and user, the credential binding's
/// closed-list service name and record types, the parsed body (or why it was refused — the handler answers and records
/// that 400) and whether the user holds the type's create permission EXACTLY (step 8 is evaluated here, refused there).
/// </summary>
public sealed record TrustedRecordCodeCaller(
    Guid TenantId,
    Guid SubjectId,
    string CallerService,
    IReadOnlySet<string> AllowedRecordTypes,
    AllocateRecordCodeRequest? Request = null,
    IReadOnlyList<string>? RequestErrors = null,
    bool HasRequiredPermission = false,
    string CorrelationId = "");
