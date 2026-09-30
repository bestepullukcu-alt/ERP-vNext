using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers;

/// <summary>
/// Repo base controller (CustomBaseController pattern) adapted to the pack §8 wire exception: success bodies are the
/// UNWRAPPED contract schema (<see cref="Response{T}.Data"/>), failures are the contract <c>Error</c> schema with the
/// request correlation id.
/// </summary>
[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult CreateActionResultInstance<T>(Response<T> response)
    {
        if (response.IsSuccessful)
        {
            return response.StatusCode == StatusCodes.Status204NoContent
                ? NoContent()
                : StatusCode(response.StatusCode, response.Data);
        }

        return ContractFailure(
            response.StatusCode,
            response.ErrorCode,
            response.Errors.Count > 0 ? string.Join(" ", response.Errors) : "The request could not be processed.",
            response.ErrorDetails);
    }

    protected IActionResult ContractFailure(
        int statusCode,
        string? code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        var correlation = HttpContext.RequestServices.GetRequiredService<ICorrelationContext>();
        return StatusCode(statusCode, ContractError.Create(code, message, correlation.CorrelationId, details));
    }
}
