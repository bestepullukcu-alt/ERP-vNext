using Diten.MdmService.Application.Authorization;
using Diten.MdmService.Application.Features.Products;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllProductsQuery());
        return Ok(new { data = result });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var id = await _mediator.Send(request);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        request.Id = id;
        var updated = await _mediator.Send(request);
        return updated ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/lifecycle")]
    public async Task<IActionResult> ChangeLifecycle(Guid id, [FromBody] ChangeProductLifecycleRequest request)
    {
        var currentProduct = await _mediator.Send(new GetProductByIdQuery(id));
        if (currentProduct is null)
        {
            return NotFound();
        }

        request.Id = id;
        request.ChangedBy = GetCurrentUserIdentifier();

        if (string.Equals(currentProduct.LifecycleStateCode, "OBSOLETE", StringComparison.OrdinalIgnoreCase)
            && IsActiveLifecycleRequest(request)
            && !HasPermission(ProductPermissions.Products.Restore))
        {
            return Forbid();
        }

        var updated = await _mediator.Send(request);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteProductRequest(id));
        return NoContent();
    }

    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteProductsRequest request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }

    private bool HasPermission(string permission)
    {
        if (User.IsInRole("SuperAdmin"))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        foreach (var claim in User.Claims)
        {
            var claimValue = claim.Value?.Trim();
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                continue;
            }

            if (string.Equals(claimValue, permission, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var token in claimValue.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(token, permission, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (claimValue.StartsWith("[", StringComparison.Ordinal)
                && claimValue.IndexOf(permission, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsActiveLifecycleRequest(ChangeProductLifecycleRequest request)
    {
        return request.LifecycleStateId == Guid.Parse("40000000-0000-0000-0000-000000000002");
    }

    private string GetCurrentUserIdentifier()
    {
        return User.Claims.FirstOrDefault(x =>
                x.Type == "sub"
                || x.Type == "nameid"
                || x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
                || x.Type == "email"
                || x.Type == "preferred_username"
                || x.Type == "name")?.Value
            ?? "unknown";
    }
}
