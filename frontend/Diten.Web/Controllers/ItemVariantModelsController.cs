using Diten.Web.Models.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
public sealed class ItemVariantModelsController : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new List<ItemVariantModelAdminViewModel>());
}
