using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

public class CountriesController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Create() => View();
    public IActionResult Edit(Guid id) => View();
}
