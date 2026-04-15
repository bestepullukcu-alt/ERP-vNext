using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

public abstract class EnterpriseStrategyApiControllerBase : CustomBaseController
{
    protected ActionResult HandleResult<T>(Response<T> response, string correlationId)
    {
        if (response.Success)
        {
            response.CorrelationId = string.IsNullOrWhiteSpace(response.CorrelationId) ? correlationId : response.CorrelationId;
            response.StatusCode = response.StatusCode == 0 ? StatusCodes.Status200OK : response.StatusCode;
            return CreateActionResultInstance(response);
        }

        response.CorrelationId = string.IsNullOrWhiteSpace(response.CorrelationId) ? correlationId : response.CorrelationId;
        response.StatusCode = response.StatusCode switch
        {
            > 0 and not StatusCodes.Status200OK => response.StatusCode,
            _ => (response.Error?.Code ?? string.Empty) switch
            {
                EnterpriseStrategyErrorCodes.ValidationError => StatusCodes.Status400BadRequest,
                EnterpriseStrategyErrorCodes.NotFound => StatusCodes.Status404NotFound,
                EnterpriseStrategyErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
                EnterpriseStrategyErrorCodes.StaleVersion => StatusCodes.Status409Conflict,
                EnterpriseStrategyErrorCodes.DependencyUnavailable => StatusCodes.Status503ServiceUnavailable,
                EnterpriseStrategyErrorCodes.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            }
        };

        response.Error ??= new ResponseError
        {
            Code = EnterpriseStrategyErrorCodes.InternalError,
            Details = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        return CreateActionResultInstance(response);
    }
}
