using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/local-test/v1/process-modeling")]
public sealed class ProcessModelingLocalTestController : ControllerBase
{
    [HttpGet("models")]
    public IActionResult Models() => FailClosed(ProcessModelingPermissions.ExactPermissions[8]);

    [HttpGet("models/{id:guid}")]
    public IActionResult Model(Guid id) =>
        id == Guid.Empty ? BadRequestResult() : FailClosed(ProcessModelingPermissions.ExactPermissions[8]);

    [HttpPost("models")]
    public IActionResult CreateModel([FromBody] JsonElement body) =>
        Write(body, ProcessModelingPermissions.ExactPermissions[9]);

    [HttpPut("models/{id:guid}")]
    public IActionResult UpdateModel(Guid id, [FromBody] JsonElement body) =>
        Write(id, body, ProcessModelingPermissions.ExactPermissions[10]);

    [HttpPut("model-versions/{id:guid}/draft-content")]
    public IActionResult UpdateDraft(Guid id, [FromBody] JsonElement body) =>
        Write(id, body, ProcessModelingPermissions.ExactPermissions[10]);

    [HttpPost("model-versions/{id:guid}/request-review")]
    public IActionResult RequestReview(Guid id, [FromBody] JsonElement body) =>
        Write(id, body, ProcessModelingPermissions.ExactPermissions[11]);

    [HttpPost("model-versions/{id:guid}/return-to-draft")]
    public IActionResult ReturnToDraft(Guid id, [FromBody] JsonElement body) =>
        Write(id, body, ProcessModelingPermissions.ExactPermissions[12]);

    [HttpPost("model-versions/{id:guid}/publish")]
    public IActionResult Publish(Guid id, [FromBody] JsonElement body) =>
        Write(id, body, ProcessModelingPermissions.ExactPermissions[13]);

    [HttpPost("model-versions/{id:guid}/retire")]
    public IActionResult Retire(Guid id, [FromBody] JsonElement body) =>
        Write(id, body, ProcessModelingPermissions.ExactPermissions[14]);

    [HttpPost("models/{id:guid}/revisions")]
    public IActionResult CreateRevision(Guid id, [FromBody] JsonElement body) =>
        Write(id, body, ProcessModelingPermissions.ExactPermissions[15]);

    private IActionResult Write(JsonElement body, string permission) =>
        body.ValueKind != JsonValueKind.Object ? BadRequestResult() : FailClosed(permission);

    private IActionResult Write(Guid id, JsonElement body, string permission) =>
        id == Guid.Empty || body.ValueKind != JsonValueKind.Object
            ? BadRequestResult()
            : FailClosed(permission);

    private IActionResult FailClosed(string permission)
    {
        if (!User.FindAll("permission").Any(claim =>
                string.Equals(claim.Value, permission, StringComparison.Ordinal)))
            return Envelope(StatusCodes.Status403Forbidden, "process_model_permission_denied");

        return Envelope(
            StatusCodes.Status503ServiceUnavailable,
            ProcessModelingErrors.ProviderUnavailable);
    }

    private IActionResult BadRequestResult() =>
        Envelope(StatusCodes.Status400BadRequest, "process_modeling_bad_request");

    private ObjectResult Envelope(int statusCode, string reasonCode) =>
        StatusCode(statusCode, new
        {
            data = (object?)null,
            isSuccessful = false,
            statusCode,
            errors = Array.Empty<string>(),
            reason_code = reasonCode,
            correlation_id = HttpContext.TraceIdentifier
        });
}
