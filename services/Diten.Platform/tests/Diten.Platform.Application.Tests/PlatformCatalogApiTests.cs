using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Features.ModuleCatalog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Infrastructure.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class PlatformCatalogApiTests : IClassFixture<PlatformCatalogApiTests.PlatformCatalogApiFactory>
{
    private readonly PlatformCatalogApiFactory _factory;

    public PlatformCatalogApiTests(PlatformCatalogApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CatalogEndpoints_ShouldSupportGoldenFlow_AndRejectInvalidRows()
    {
        var client = _factory.CreateAuthorizedClient();

        var validPayload = new
        {
            rows = new[]
            {
                new
                {
                    moduleId = "MOD-0014",
                    domainLandscape = "Platform Shared Services",
                    suitePlatform = "Global Registry",
                    capabilityGroup = "Catalog Foundation",
                    moduleName = "Module Boundary Registry",
                    dependencyGate = "Ready",
                    deliveryOutcome = "Global catalog foundation",
                    placement = "Platform Admin",
                    supportModel = "Central Ops",
                    isPlatformCore = false,
                    isTenantAssignable = true,
                    status = "Active"
                }
            }
        };

        var importResponse = await client.PostAsJsonAsync("/api/platform/catalog/import", validPayload);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var importResult = await ReadEnvelopeAsync<ModuleCatalogImportResultDto>(importResponse);
        Assert.Equal(1, importResult.CreatedCount);

        var modulesResponse = await client.GetAsync("/api/platform/catalog/modules");
        Assert.Equal(HttpStatusCode.OK, modulesResponse.StatusCode);
        var modules = await ReadEnvelopeAsync<ModuleDefinitionListResultDto>(modulesResponse);
        Assert.Single(modules.Items);

        var detailResponse = await client.GetAsync("/api/platform/catalog/modules/MOD-0014");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await ReadEnvelopeAsync<ModuleDefinitionDetailDto>(detailResponse);
        Assert.Equal("Module Boundary Registry", detail.ModuleName);

        var createPageResponse = await client.PostAsJsonAsync("/api/platform/catalog/modules/MOD-0014/pages", new
        {
            moduleId = "MOD-0014",
            pageCode = "PRODUCT_LIST",
            pageName = "Product List",
            pageType = "List",
            routePath = "products",
            requiredPermissionKey = "mdm.products.read",
            isNavigationCandidate = true,
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, createPageResponse.StatusCode);
        var createdPage = await ReadEnvelopeAsync<ModulePageDefinitionDto>(createPageResponse);
        Assert.Equal("PRODUCT_LIST", createdPage.PageCode);
        Assert.Equal("/products", createdPage.RoutePath);

        var pagesResponse = await client.GetAsync("/api/platform/catalog/modules/MOD-0014/pages");
        Assert.Equal(HttpStatusCode.OK, pagesResponse.StatusCode);
        var pages = await ReadEnvelopeAsync<IReadOnlyList<ModulePageDefinitionDto>>(pagesResponse);
        Assert.Single(pages);

        var pageByCodeResponse = await client.GetAsync("/api/platform/catalog/modules/MOD-0014/pages/PRODUCT_LIST");
        Assert.Equal(HttpStatusCode.OK, pageByCodeResponse.StatusCode);
        var pageByCode = await ReadEnvelopeAsync<ModulePageDefinitionDto>(pageByCodeResponse);
        Assert.Equal("Product List", pageByCode.PageName);

        var updatePageResponse = await client.PutAsJsonAsync("/api/platform/catalog/modules/MOD-0014/pages/PRODUCT_LIST", new
        {
            moduleId = "MOD-0014",
            pageCode = "PRODUCT_LIST",
            pageName = "Products",
            pageType = "List",
            routePath = "/products/list",
            isNavigationCandidate = false,
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, updatePageResponse.StatusCode);
        var updatedPage = await ReadEnvelopeAsync<ModulePageDefinitionDto>(updatePageResponse);
        Assert.Equal("Products", updatedPage.PageName);
        Assert.False(updatedPage.IsNavigationCandidate);

        var hierarchyResponse = await client.GetAsync("/api/platform/catalog/hierarchy");
        Assert.Equal(HttpStatusCode.OK, hierarchyResponse.StatusCode);
        var hierarchy = await ReadEnvelopeAsync<ModuleCatalogHierarchyDto>(hierarchyResponse);
        Assert.Equal(1, hierarchy.Summary.TotalModules);

        var secondImportResponse = await client.PostAsJsonAsync("/api/platform/catalog/import", validPayload);
        var secondImport = await ReadEnvelopeAsync<ModuleCatalogImportResultDto>(secondImportResponse);
        Assert.Equal(0, secondImport.CreatedCount);
        Assert.Equal(1, secondImport.SkippedCount);

        var invalidPayload = new
        {
            rows = new[]
            {
                new
                {
                    moduleId = "MOD-BROKEN",
                    domainLandscape = "Broken Domain",
                    suitePlatform = "Broken Suite",
                    capabilityGroup = "Broken Capability",
                    moduleName = "Broken Module",
                    status = "UnknownStatus"
                }
            }
        };

        var invalidResponse = await client.PostAsJsonAsync("/api/platform/catalog/import", invalidPayload);
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        var invalidResult = await ReadEnvelopeAsync<ModuleCatalogImportResultDto>(invalidResponse);
        Assert.Equal(1, invalidResult.FailedCount);
        Assert.Single(invalidResult.FailedRows);

        var hierarchyAfterInvalidResponse = await client.GetAsync("/api/platform/catalog/hierarchy");
        Assert.Equal(HttpStatusCode.OK, hierarchyAfterInvalidResponse.StatusCode);
        var hierarchyAfterInvalid = await ReadEnvelopeAsync<ModuleCatalogHierarchyDto>(hierarchyAfterInvalidResponse);
        Assert.Equal(1, hierarchyAfterInvalid.Summary.TotalDomains);
        Assert.Equal(1, hierarchyAfterInvalid.Summary.TotalSuites);
        Assert.Equal(1, hierarchyAfterInvalid.Summary.TotalCapabilityGroups);
        Assert.Equal(1, hierarchyAfterInvalid.Summary.TotalModules);
    }

    [Fact]
    public async Task Swagger_ShouldExposePlatformCatalogRoutes_AndNotExposeModRoute()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/platform/catalog/modules", out _));
        Assert.True(paths.TryGetProperty("/api/platform/catalog/modules/{moduleId}/pages", out _));
        Assert.True(paths.TryGetProperty("/api/platform/catalog/modules/{moduleId}/pages/{pageCode}", out _));
        Assert.False(paths.TryGetProperty("/api/mod0014/modules", out _));
        Assert.DoesNotContain(paths.EnumerateObject(), path => path.Name.Contains("/api/mod0014", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FailurePath_ShouldReturnBadRequest_ForInvalidOperations()
    {
        var client = _factory.CreateAuthorizedClient();

        // 1. Create valid hierarchy
        var domainCode = $"D-{Guid.NewGuid():N}";
        var domainRes = await client.PostAsJsonAsync("/api/platform/catalog/domain-landscapes", new { code = domainCode, name = "Test Domain", description = "", isActive = true });
        var domain = await ReadEnvelopeAsync<DomainLandscapeDto>(domainRes);

        var suiteCode = $"S-{Guid.NewGuid():N}";
        var suiteRes = await client.PostAsJsonAsync("/api/platform/catalog/suite-platforms", new { domainLandscapeId = domain.Id, code = suiteCode, name = "Test Suite", description = "", isActive = true });
        var suite = await ReadEnvelopeAsync<SuitePlatformDto>(suiteRes);

        var capCode = $"C-{Guid.NewGuid():N}";
        var capRes = await client.PostAsJsonAsync("/api/platform/catalog/capability-groups", new { domainLandscapeId = domain.Id, suitePlatformId = suite.Id, code = capCode, name = "Test Capability", description = "", isActive = true });
        var capability = await ReadEnvelopeAsync<CapabilityGroupDto>(capRes);

        var moduleId = $"MOD-{Guid.NewGuid():N}";
        var moduleRes = await client.PostAsJsonAsync("/api/platform/catalog/modules", new { moduleId = moduleId, moduleName = "Test Module", domainLandscapeId = domain.Id, suitePlatformId = suite.Id, capabilityGroupId = capability.Id, status = "Active", isPlatformCore = false, isTenantAssignable = true });
        var module = await ReadEnvelopeAsync<ModuleDefinitionDetailDto>(moduleRes);

        // 2. Duplicate validation separation
        // DomainLandscape global unique
        var dupDomainRes = await client.PostAsJsonAsync("/api/platform/catalog/domain-landscapes", new { code = domainCode, name = "Dup Domain", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, dupDomainRes.StatusCode);

        // SuitePlatform code unique within domain
        var dupSuiteRes = await client.PostAsJsonAsync("/api/platform/catalog/suite-platforms", new { domainLandscapeId = domain.Id, code = suiteCode, name = "Dup Suite", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, dupSuiteRes.StatusCode);

        // CapabilityGroup code unique within suite
        var dupCapRes = await client.PostAsJsonAsync("/api/platform/catalog/capability-groups", new { domainLandscapeId = domain.Id, suitePlatformId = suite.Id, code = capCode, name = "Dup Capability", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, dupCapRes.StatusCode);

        // ModuleId global unique
        var dupModuleRes = await client.PostAsJsonAsync("/api/platform/catalog/modules", new { moduleId = moduleId, moduleName = "Dup Module", domainLandscapeId = domain.Id, suitePlatformId = suite.Id, capabilityGroupId = capability.Id, status = "Active", isPlatformCore = false, isTenantAssignable = true });
        Assert.Equal(HttpStatusCode.BadRequest, dupModuleRes.StatusCode);

        // 3. Hierarchy validation
        var invalidSuiteRes = await client.PostAsJsonAsync("/api/platform/catalog/suite-platforms", new { domainLandscapeId = Guid.NewGuid(), code = "S-INVALID", name = "S", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalidSuiteRes.StatusCode);

        var invalidCapRes = await client.PostAsJsonAsync("/api/platform/catalog/capability-groups", new { domainLandscapeId = domain.Id, suitePlatformId = Guid.NewGuid(), code = "C-INVALID", name = "C", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCapRes.StatusCode);

        var invalidModuleRes = await client.PostAsJsonAsync("/api/platform/catalog/modules", new { moduleId = "MOD-INV-1", moduleName = "Inv", domainLandscapeId = domain.Id, suitePlatformId = suite.Id, capabilityGroupId = Guid.NewGuid(), status = "Active", isPlatformCore = false, isTenantAssignable = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalidModuleRes.StatusCode);

        var mismatchModuleRes = await client.PostAsJsonAsync("/api/platform/catalog/modules", new { moduleId = "MOD-INV-2", moduleName = "Inv", domainLandscapeId = Guid.NewGuid(), suitePlatformId = suite.Id, capabilityGroupId = capability.Id, status = "Active", isPlatformCore = false, isTenantAssignable = true });
        Assert.Equal(HttpStatusCode.BadRequest, mismatchModuleRes.StatusCode);

        // 4. ModuleId immutability
        var putModuleRes = await client.PutAsJsonAsync($"/api/platform/catalog/modules/{moduleId}", new { moduleId = "MOD-NEW-ID", moduleName = "Updated Module", domainLandscapeId = domain.Id, suitePlatformId = suite.Id, capabilityGroupId = capability.Id, status = "Active", isPlatformCore = false, isTenantAssignable = true });
        Assert.Equal(HttpStatusCode.BadRequest, putModuleRes.StatusCode);

        var getModuleRes = await client.GetAsync($"/api/platform/catalog/modules/{moduleId}");
        var currentModule = await ReadEnvelopeAsync<ModuleDefinitionDetailDto>(getModuleRes);
        Assert.Equal("Test Module", currentModule.ModuleName);

        // 5. Module page failure paths
        var pageRes = await client.PostAsJsonAsync($"/api/platform/catalog/modules/{moduleId}/pages", new { moduleId, pageCode = "PRODUCT_LIST", pageName = "Product List", pageType = "List" });
        Assert.Equal(HttpStatusCode.Created, pageRes.StatusCode);

        var dupPageRes = await client.PostAsJsonAsync($"/api/platform/catalog/modules/{moduleId}/pages", new { moduleId, pageCode = "PRODUCT_LIST", pageName = "Duplicate", pageType = "List" });
        Assert.Equal(HttpStatusCode.BadRequest, dupPageRes.StatusCode);

        var invalidModulePageRes = await client.PostAsJsonAsync("/api/platform/catalog/modules/MOD-MISSING/pages", new { moduleId = "MOD-MISSING", pageCode = "PRODUCT_LIST", pageName = "Product List", pageType = "List" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidModulePageRes.StatusCode);

        var mismatchModulePageRes = await client.PutAsJsonAsync($"/api/platform/catalog/modules/{moduleId}/pages/PRODUCT_LIST", new { moduleId = "MOD-OTHER", pageCode = "PRODUCT_LIST", pageName = "Mismatch", pageType = "List" });
        Assert.Equal(HttpStatusCode.BadRequest, mismatchModulePageRes.StatusCode);

        var mismatchPageCodeRes = await client.PutAsJsonAsync($"/api/platform/catalog/modules/{moduleId}/pages/PRODUCT_LIST", new { moduleId, pageCode = "OTHER_PAGE", pageName = "Mismatch", pageType = "List" });
        Assert.Equal(HttpStatusCode.BadRequest, mismatchPageCodeRes.StatusCode);

        var unchangedPageRes = await client.GetAsync($"/api/platform/catalog/modules/{moduleId}/pages/PRODUCT_LIST");
        var unchangedPage = await ReadEnvelopeAsync<ModulePageDefinitionDto>(unchangedPageRes);
        Assert.Equal("Product List", unchangedPage.PageName);
    }

    private static async Task<T> ReadEnvelopeAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return JsonSerializer.Deserialize<T>(
            document.RootElement.GetProperty("data").GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    public sealed class PlatformCatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databaseName = $"DitenPlatformCatalogTests_{Guid.NewGuid():N}";
        private readonly string _secret = "DitenErpVNextSuperSecretKeyAtLeast256Bits_ChangeInProduction_2026!";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDbSettings:ConnectionString"] = "mongodb://localhost:27017",
                    ["MongoDbSettings:DatabaseName"] = _databaseName,
                    ["JwtSettings:Secret"] = _secret,
                    ["JwtSettings:Issuer"] = "diten-auth-service",
                    ["JwtSettings:Audience"] = "diten-erp"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                var mongoClientSettings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
                mongoClientSettings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
                var mongoClient = new MongoClient(mongoClientSettings);
                var database = mongoClient.GetDatabase(_databaseName);

                var contextDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IPlatformDbContext));
                if (contextDesc != null) services.Remove(contextDesc);

                var dbDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IMongoDatabase));
                if (dbDesc != null) services.Remove(dbDesc);

                services.AddSingleton<IPlatformDbContext>(new Diten.Platform.Infrastructure.Persistence.PlatformDbContext(mongoClient, database));
                services.AddScoped<IMongoDatabase>(_ => database);
            });
        }

        public HttpClient CreateAuthorizedClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
            return client;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public new async Task DisposeAsync()
        {
            using var scope = Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
            await database.Client.DropDatabaseAsync(_databaseName);
        }

        private string CreateToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                    new Claim("actor_type", "platform_admin")
                }),
                Expires = DateTime.UtcNow.AddMinutes(30),
                Issuer = "diten-auth-service",
                Audience = "diten-erp",
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(descriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
