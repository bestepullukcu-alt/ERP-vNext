using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Diten.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers
{
    [Authorize]
    public class CountriesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _gatewayUrl;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public CountriesController(
            HttpClient httpClient,
            IConfiguration configuration,
            IStringLocalizer<SharedResource> localizer)
        {
            _httpClient = httpClient;
            _localizer = localizer;
            _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        }

        private void AddAuthHeaders()
        {
            var token = Request.Cookies["access_token"];
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var tenantId = User.FindFirst("tenant_id")?.Value ?? "00000000-0000-0000-0000-000000000001";
            if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            {
                _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
            }
            _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }

        [HttpGet]
        public Task<IActionResult> Index()
        {
            return Task.FromResult<IActionResult>(View(new List<CountryViewModel>()));
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateCountryViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCountryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            AddAuthHeaders();
            try
            {
                var payload = new
                {
                    name = model.Name,
                    iso2Code = model.Iso2Code,
                    iso3Code = model.Iso3Code,
                    phoneCode = model.PhoneCode
                };

                var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/countries", payload);
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
        public async Task<IActionResult> Edit(Guid id)
        {
            AddAuthHeaders();
            try
            {
                var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/countries/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var entity = await response.Content.ReadFromJsonAsync<CountryViewModel>();
                    if (entity != null)
                    {
                        var vm = new CreateCountryViewModel
                        {
                            Id = entity.Id,
                            Name = entity.Name,
                            Iso2Code = entity.Iso2Code,
                            Iso3Code = entity.Iso3Code,
                            PhoneCode = entity.PhoneCode,
                            IsActive = entity.IsActive
                        };
                        return View("Create", vm);
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
        public async Task<IActionResult> Edit(Guid id, CreateCountryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            AddAuthHeaders();
            try
            {
                var payload = new
                {
                    name = model.Name,
                    iso2Code = model.Iso2Code,
                    iso3Code = model.Iso3Code,
                    phoneCode = model.PhoneCode,
                    isActive = model.IsActive
                };

                var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/countries/{id}", payload);
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

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            AddAuthHeaders();
            try
            {
                var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/countries/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var entity = await response.Content.ReadFromJsonAsync<CountryViewModel>();
                    if (entity != null)
                    {
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
    }
}

