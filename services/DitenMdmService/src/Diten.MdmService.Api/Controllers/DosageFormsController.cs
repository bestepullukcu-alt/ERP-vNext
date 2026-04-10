using Diten.MdmService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dosage-forms")]
public sealed class DosageFormsController : ControllerBase
{
    private readonly IItemLookupRepository _lookupRepository;

    public DosageFormsController(IItemLookupRepository lookupRepository)
    {
        _lookupRepository = lookupRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await _lookupRepository.EnsureSeedDataAsync();
        var result = await _lookupRepository.GetDosageFormsAsync();
        return Ok(new { data = result });
    }
}
