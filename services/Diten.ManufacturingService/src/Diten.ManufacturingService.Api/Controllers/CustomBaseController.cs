using Diten.ManufacturingService.Application.Common;
using Diten.Shared.Core;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManufacturingService.Api.Controllers;

/// <summary>
/// <c>Response&lt;T&gt;</c> → BOM contract tel biçimi (pack §8 yanıt biçimi istisnası). Başarı: <c>Data</c> doğrudan
/// (frozen <c>BomView</c> zarfsız); hata: <c>{ error: { code, message, correlationId }, contractVersion }</c>.
/// Handler hata kodu (<c>UNKNOWN_BOM</c> …) döndüyse o; doğrulama mesajı döndüyse <c>INVALID_REQUEST</c> + ilk mesaj.
/// </summary>
[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult CreateActionResultInstance<T>(Response<T> response)
    {
        if (response.IsSuccessful)
        {
            return response.StatusCode == 204 ? NoContent() : StatusCode(response.StatusCode, response.Data);
        }

        var first = response.Errors.FirstOrDefault() ?? BomErrorCodes.InvalidRequest;
        var isCode = first.Length > 0 && first.All(c => c is >= 'A' and <= 'Z' or '_');
        var code = isCode ? first : BomErrorCodes.InvalidRequest;
        var message = isCode ? BomErrorCodes.Message(code) : string.Join(" ", response.Errors);
        return StatusCode(response.StatusCode, ContractError(code, message));
    }

    protected object ContractError(string code, string message) => ContractErrors.Body(code, message, HttpContext);
}

public static class ContractErrors
{
    public static object Body(string code, string message, HttpContext http) => new
    {
        error = new
        {
            code,
            message,
            correlationId = http.RequestServices.GetRequiredService<ICorrelationContext>().CorrelationId
        },
        contractVersion = BomContractVersion
    };

    public const string BomContractVersion = "v1";
}
