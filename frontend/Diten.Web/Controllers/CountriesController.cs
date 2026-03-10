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
            return View();
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            return View("Create");
        }

        [HttpGet("Details/{id:guid}")]
        public async Task<IActionResult> Details(Guid id)
        {
            return View();
        }
    }
}
