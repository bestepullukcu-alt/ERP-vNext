using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers
{
    [Authorize]
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
            
            // Gateway URL default or from configuration. 
            _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000"; 
        }

        private void AddAuthHeaders()
        {
            var token = Request.Cookies["access_token"];
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Set Tenant ID from user claims
            var tenantId = User.FindFirst("tenant_id")?.Value ?? "00000000-0000-0000-0000-000000000001";
            if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            {
                _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
            }
            _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Set basic display info even if API fails as page uses AJAX
            ViewBag.TenantId = User.FindFirst("tenant_id")?.Value ?? "00000000-0000-0000-0000-000000000001";
            
            return View(new List<LegalEntityViewModel>());
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

            AddAuthHeaders();
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/legal-entities", model);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "RecordCreated";
                    return RedirectToAction(nameof(Index));
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
                }

                ModelState.AddModelError(string.Empty, _localizer["GatewayError"].Value);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, _localizer["GatewayError"].Value);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            AddAuthHeaders();
            try
            {
                var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/legal-entities/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var entity = await response.Content.ReadFromJsonAsync<CreateLegalEntityViewModel>();
                    if (entity != null)
                    {
                        entity.Id = id;
                        return View(entity);
                    }
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
                }

                TempData["ErrorMessage"] = _localizer["RecordNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = _localizer["GatewayError"].Value;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            AddAuthHeaders();
            try
            {
                var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/legal-entities/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var entity = await response.Content.ReadFromJsonAsync<CreateLegalEntityViewModel>();
                    if (entity != null)
                    {
                        entity.Id = id;
                        return View("Create", entity);
                    }
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
                }
                
                TempData["ErrorMessage"] = _localizer["RecordNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = _localizer["GatewayError"].Value;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Guid id, CreateLegalEntityViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            AddAuthHeaders();
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/legal-entities/{id}", model);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "RecordUpdated";
                    return RedirectToAction(nameof(Index));
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
                }

                ModelState.AddModelError(string.Empty, _localizer["GatewayError"].Value);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, _localizer["GatewayError"].Value);
            }

            return View("Create", model);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id)
        {
            AddAuthHeaders();
            try
            {
                var response = await _httpClient.DeleteAsync($"{_gatewayUrl}/api/legal-entities/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Record deleted successfully." });
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Json(new { success = false, message = "Unauthorized", redirect = true });
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
