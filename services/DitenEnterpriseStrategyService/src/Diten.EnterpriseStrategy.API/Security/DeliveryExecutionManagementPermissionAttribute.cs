using Diten.Application.Common.Models;
using Diten.Application.DeliveryExecutionManagement.Shared;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.WebAPI.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DeliveryExecutionManagementPermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permission;

    public DeliveryExecutionManagementPermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var service = context.HttpContext.RequestServices.GetRequiredService<IDeliveryExecutionManagementAuthorizationService>();
        var correlation = context.HttpContext.RequestServices.GetRequiredService<ICorrelationContextAccessor>();

        if (!await service.HasPermissionAsync(_permission, context.HttpContext.User, context.HttpContext.RequestAborted))
        {
            context.Result = new ObjectResult(
                Response<object>.Fail(
                    EnterpriseStrategyErrorCodes.Forbidden,
                    StatusCodes.Status403Forbidden,
                    correlation.CorrelationId))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
