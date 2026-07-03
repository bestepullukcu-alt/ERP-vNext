using Diten.Platform.Application.Features.Navigation;
using Diten.Platform.Application.Features.Navigation.Commands;
using Diten.Platform.Application.Features.Navigation.Handlers;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Navigation;

public sealed class ReplaceTenantNavPreferencesCommandHandlerTests
{
    private static readonly IReadOnlyList<TenantNavDomainPreferenceDto> NoDomains = Array.Empty<TenantNavDomainPreferenceDto>();

    [Fact]
    public async Task Replace_PersistsOnlyEntitledModules_AndIgnoresNonEntitledOrBlank()
    {
        var tenantId = Guid.NewGuid();

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "ALPHA", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(x => x.HasAccessAsync(tenantId, "BETA", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(x => x.HasAccessAsync(tenantId, "GHOST", It.IsAny<CancellationToken>())).ReturnsAsync(false); // not entitled

        IReadOnlyCollection<TenantNavPreference>? captured = null;
        var repo = new Mock<ITenantNavPreferenceRepository>();
        repo.Setup(x => x.ReplaceForTenantAsync(tenantId, It.IsAny<IReadOnlyCollection<TenantNavPreference>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<TenantNavPreference>, CancellationToken>((_, items, _) => captured = items)
            .Returns(Task.CompletedTask);

        var domainRepo = new Mock<ITenantNavDomainPreferenceRepository>();
        domainRepo.Setup(x => x.ReplaceForTenantAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<TenantNavDomainPreference>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ReplaceTenantNavPreferencesCommandHandler(repo.Object, domainRepo.Object, access.Object);

        var command = new ReplaceTenantNavPreferencesCommand(tenantId, new List<TenantNavPreferenceDto>
        {
            new("ALPHA", 1, false, "Custom Alpha"),
            new("BETA", 2, true, null),
            new("GHOST", 3, true, "Hidden"),   // not entitled → dropped
            new("  ", null, false, null)        // blank code → dropped
        }, NoDomains);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(captured);
        var codes = captured!.Select(x => x.ModuleCode).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "ALPHA", "BETA" }, codes);

        var alpha = captured!.Single(x => x.ModuleCode == "ALPHA");
        Assert.Equal(tenantId, alpha.TenantId);
        Assert.Equal(1, alpha.SortOrder);
        Assert.False(alpha.IsHidden);
        Assert.Equal("Custom Alpha", alpha.DisplayNameOverride);

        var beta = captured!.Single(x => x.ModuleCode == "BETA");
        Assert.True(beta.IsHidden);
        Assert.Null(beta.DisplayNameOverride); // null override preserved as null
    }

    [Fact]
    public async Task Replace_PersistsDomainPreferences_DedupedAndBlankDropped()
    {
        var tenantId = Guid.NewGuid();
        var access = new Mock<ITenantModuleAccessService>();

        var repo = new Mock<ITenantNavPreferenceRepository>();
        repo.Setup(x => x.ReplaceForTenantAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<TenantNavPreference>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        IReadOnlyCollection<TenantNavDomainPreference>? capturedDomains = null;
        var domainRepo = new Mock<ITenantNavDomainPreferenceRepository>();
        domainRepo.Setup(x => x.ReplaceForTenantAsync(tenantId, It.IsAny<IReadOnlyCollection<TenantNavDomainPreference>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<TenantNavDomainPreference>, CancellationToken>((_, items, _) => capturedDomains = items)
            .Returns(Task.CompletedTask);

        var handler = new ReplaceTenantNavPreferencesCommandHandler(repo.Object, domainRepo.Object, access.Object);

        var command = new ReplaceTenantNavPreferencesCommand(tenantId, Array.Empty<TenantNavPreferenceDto>(), new List<TenantNavDomainPreferenceDto>
        {
            new("SALES", 0, "Money"),
            new("SALES", 1, "Sales Renamed"),   // duplicate domain → last wins
            new("  ", 2, "Blank")                // blank domain → dropped
        });

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(capturedDomains);
        var sales = Assert.Single(capturedDomains!);
        Assert.Equal("SALES", sales.DomainCode);
        Assert.Equal(1, sales.SortOrder);                 // last-wins
        Assert.Equal("Sales Renamed", sales.DisplayNameOverride);
        Assert.Equal(tenantId, sales.TenantId);
    }

    [Fact]
    public async Task Replace_WithEmptySet_ClearsBothModuleAndDomainPreferences()
    {
        var tenantId = Guid.NewGuid();
        var access = new Mock<ITenantModuleAccessService>();

        var repo = new Mock<ITenantNavPreferenceRepository>();
        repo.Setup(x => x.ReplaceForTenantAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<TenantNavPreference>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var domainRepo = new Mock<ITenantNavDomainPreferenceRepository>();
        domainRepo.Setup(x => x.ReplaceForTenantAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<TenantNavDomainPreference>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ReplaceTenantNavPreferencesCommandHandler(repo.Object, domainRepo.Object, access.Object);

        var response = await handler.Handle(
            new ReplaceTenantNavPreferencesCommand(tenantId, Array.Empty<TenantNavPreferenceDto>(), NoDomains),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        repo.Verify(x => x.ReplaceForTenantAsync(tenantId, It.IsAny<IReadOnlyCollection<TenantNavPreference>>(), It.IsAny<CancellationToken>()), Times.Once);
        domainRepo.Verify(x => x.ReplaceForTenantAsync(tenantId, It.IsAny<IReadOnlyCollection<TenantNavDomainPreference>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
