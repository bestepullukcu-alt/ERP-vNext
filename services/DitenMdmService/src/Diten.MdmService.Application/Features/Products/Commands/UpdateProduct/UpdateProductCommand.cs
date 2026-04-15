using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommand : IRequest<Response<bool>>
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public int ProductType { get; set; }
    public Guid CategoryId { get; set; }
    public Guid LifecycleStateId { get; set; }
    public bool IsSaleable { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}
