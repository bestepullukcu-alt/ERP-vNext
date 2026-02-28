using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers
{
    [AllowAnonymous]
    public class LegalEntitiesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _gatewayUrl;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public LegalEntitiesController(
            HttpClient httpClient,
            IConfiguration configuration,
            IStringLocalizer<SharedResource> localizer)
        {
            _httpClient = httpClient;
            _localizer = localizer;
            
            if (!_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", "00000000-0000-0000-0000-000000000001");
            }

            // Gateway URL default or from configuration. 
            _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000"; 
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var legalEntities = new List<LegalEntityViewModel>();
            try
            {
                var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/legal-entities");
                if (response.IsSuccessStatusCode)
                {
                    legalEntities = await response.Content.ReadFromJsonAsync<List<LegalEntityViewModel>>() ?? new List<LegalEntityViewModel>();
                }
                else
                {
                    ViewBag.ErrorMessage = _localizer["FailedToLoadData"].Value;
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = _localizer["GatewayError"].Value;
            }

            return View(legalEntities);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateLegalEntityViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateLegalEntityViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/legal-entities", model);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "RecordCreated";
                    return RedirectToAction(nameof(Index));
                }
                
                ModelState.AddModelError(string.Empty, _localizer["GatewayError"].Value);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, _localizer["GatewayError"].Value);
            }

            return View(model);
        }
        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_gatewayUrl}/api/legal-entities/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Record deleted successfully." });
                }
                return Json(new { success = false, message = _localizer["GatewayError"].Value });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = _localizer["GatewayError"].Value });
            }
        }
    }
}
