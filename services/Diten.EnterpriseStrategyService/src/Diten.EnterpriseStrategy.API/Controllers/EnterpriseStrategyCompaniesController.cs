using Asp.Versioning;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy/companies")]
public sealed class EnterpriseStrategyCompaniesController : EnterpriseStrategyApiControllerBase
{
    [HttpGet]
    public ActionResult<Response<IReadOnlyList<CompanyReferenceDto>>> List()
    {
        var rows = EnterpriseStrategyLookupCatalog.Companies;
        return Ok(Response<IReadOnlyList<CompanyReferenceDto>>.Ok(rows, HttpContext.TraceIdentifier));
    }
}
