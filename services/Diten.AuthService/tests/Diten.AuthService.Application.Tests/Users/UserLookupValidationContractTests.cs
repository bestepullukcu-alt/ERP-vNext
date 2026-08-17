using System.Reflection;
using System.Text.Json;
using Diten.AuthService.Application.DTOs;

namespace Diten.AuthService.Application.Tests.Users;

public sealed class UserLookupValidationContractTests
{
    [Fact]
    public void ResponseDtoContainsOnlyUserIdAndReferenceable()
    {
        var properties = typeof(TenantUserLookupValidationDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(["Referenceable", "UserId"], properties);
    }

    [Fact]
    public void ResponseJsonDoesNotLeakTenantOrProfileAuthorizationOrStatusDetails()
    {
        var dto = new TenantUserLookupValidationDto(Guid.NewGuid(), Referenceable: true);

        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("TenantId", json);
        Assert.DoesNotContain("Email", json);
        Assert.DoesNotContain("UserName", json);
        Assert.DoesNotContain("FirstName", json);
        Assert.DoesNotContain("LastName", json);
        Assert.DoesNotContain("Profile", json);
        Assert.DoesNotContain("Roles", json);
        Assert.DoesNotContain("Permissions", json);
        Assert.DoesNotContain("Claims", json);
        Assert.DoesNotContain("IsActive", json);
        Assert.DoesNotContain("IsDeleted", json);
        Assert.Contains("UserId", json);
        Assert.Contains("Referenceable", json);
    }
}
