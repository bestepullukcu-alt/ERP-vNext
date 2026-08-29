namespace Diten.MdmService.Application.Contracts.Workflow;

public interface IProductIdentityDelegatedTokenAccessor
{
    string GetRequiredToken();
}

public sealed class ProductIdentityDelegatedTokenException : Exception
{
    public ProductIdentityDelegatedTokenException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
    public string ErrorCode { get; }
}
