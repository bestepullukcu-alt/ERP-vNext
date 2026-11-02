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
/// Who is asking, as the executor established it: the token's tenant and user, the credential binding's closed-list service
/// name and the record types that binding may ask for.
/// </summary>
public sealed record TrustedRecordCodeCaller(
    Guid TenantId,
    Guid SubjectId,
    string CallerService,
    IReadOnlySet<string> AllowedRecordTypes);
