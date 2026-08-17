using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Application.Common.Models;

public class CustomBaseController : ControllerBase
{
    private IMediator? _mediator;

    protected IMediator? Mediator => _mediator ??= HttpContext.RequestServices.GetService<IMediator>();

    public ActionResult CreateActionResultInstance<T>(Response<T> response) =>
        new ObjectResult(response)
        {
            StatusCode = response.StatusCode
        };
}
