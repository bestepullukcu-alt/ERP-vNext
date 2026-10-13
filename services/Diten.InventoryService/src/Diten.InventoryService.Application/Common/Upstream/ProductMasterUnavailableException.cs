namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>PRODUCT-MASTER could not answer → 503 <c>PRODUCT_MASTER_UNAVAILABLE</c>, nothing written.</summary>
public sealed class ProductMasterUnavailableException : Exception
{
    public ProductMasterUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
