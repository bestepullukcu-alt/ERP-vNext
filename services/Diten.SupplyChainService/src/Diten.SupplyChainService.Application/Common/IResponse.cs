namespace Diten.SupplyChainService.Application.Common;
public interface IResponse<TSelf> where TSelf : IResponse<TSelf>
{
    static abstract TSelf Fail(string code, int status, string? message = null);
}
