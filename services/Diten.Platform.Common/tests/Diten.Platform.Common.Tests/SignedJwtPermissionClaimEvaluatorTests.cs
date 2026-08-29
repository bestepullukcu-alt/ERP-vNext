using System.Security.Claims;
using Diten.Platform.Common.Authorization;
using Xunit;

namespace Diten.Platform.Common.Tests;

public sealed class SignedJwtPermissionClaimEvaluatorTests
{
    private const string RequiredPermission = "ppm.portfolios.create";
    private readonly SignedJwtPermissionClaimEvaluator _sut = new();

    [Fact]
    public void ExactLowercasePermissionAllows()
        => Assert.True(_sut.HasPermission(Principal(Permission(RequiredPermission)), RequiredPermission));

    [Theory]
    [InlineData("PPM.Portfolios.Create")]
    [InlineData("ppm.Portfolios.create")]
    public void MixedOrUppercasePermissionDenies(string grantedPermission)
        => Assert.False(_sut.HasPermission(Principal(Permission(grantedPermission)), RequiredPermission));

    [Theory]
    [InlineData("*")]
    [InlineData("ppm.*")]
    [InlineData("ppm.portfolios.*")]
    [InlineData("ppm.portfolios")]
    [InlineData("ppm.portfolios.create.extra")]
    public void WildcardPrefixAndPartialPermissionsDeny(string grantedPermission)
        => Assert.False(_sut.HasPermission(Principal(Permission(grantedPermission)), RequiredPermission));

    [Theory]
    [InlineData(" ppm.portfolios.create")]
    [InlineData("ppm.portfolios.create ")]
    [InlineData("ppm.portfolios.create\t")]
    public void PermissionWhitespaceIsNotNormalized(string grantedPermission)
        => Assert.False(_sut.HasPermission(Principal(Permission(grantedPermission)), RequiredPermission));

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\t\n")]
    public void WhitespaceOnlyRequiredPermissionDeniesEvenWhenClaimIsIdentical(string whitespace)
        => Assert.False(_sut.HasPermission(Principal(Permission(whitespace)), whitespace));

    [Fact]
    public void WhitespaceOnlyPermissionClaimDenies()
        => Assert.False(_sut.HasPermission(Principal(Permission(" ")), RequiredPermission));

    [Fact]
    public void ValidRequiredPermissionWithLeadingOrTrailingWhitespaceDenies()
    {
        var principal = Principal(Permission(RequiredPermission));

        Assert.False(_sut.HasPermission(principal, $" {RequiredPermission}"));
        Assert.False(_sut.HasPermission(principal, $"{RequiredPermission} "));
    }

    [Fact]
    public void RoleOnlyPrincipalDenies()
        => Assert.False(_sut.HasPermission(
            Principal(new Claim(ClaimTypes.Role, "SuperAdmin")),
            RequiredPermission));

    [Fact]
    public void UnauthenticatedPrincipalDenies()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [Subject(), Tenant(), Permission(RequiredPermission)]));

        Assert.False(_sut.HasPermission(principal, RequiredPermission));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void MissingInvalidOrEmptyTenantDenies(string? tenantValue)
    {
        var claims = new List<Claim> { Subject(), Permission(RequiredPermission) };
        if (tenantValue is not null)
        {
            claims.Add(new Claim(SignedJwtPermissionClaimEvaluator.TenantClaimType, tenantValue));
        }

        Assert.False(_sut.HasPermission(Authenticated(claims), RequiredPermission));
    }

    [Fact]
    public void ValidSubjectAllows()
        => Assert.True(_sut.HasPermission(Principal(Permission(RequiredPermission)), RequiredPermission));

    [Fact]
    public void MissingSubjectFallsBackToValidNameIdentifier()
    {
        var principal = PrincipalWithoutSubject(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            Permission(RequiredPermission));

        Assert.True(_sut.HasPermission(principal, RequiredPermission));
    }

    [Fact]
    public void InvalidExistingSubjectDoesNotFallBackToNameIdentifier()
    {
        var principal = Authenticated(
        [
            new Claim(SignedJwtPermissionClaimEvaluator.SubjectClaimType, "not-a-guid"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            Tenant(),
            Permission(RequiredPermission)
        ]);

        Assert.False(_sut.HasPermission(principal, RequiredPermission));
    }

    [Fact]
    public void MissingPermissionDenies()
        => Assert.False(_sut.HasPermission(Principal(), RequiredPermission));

    [Fact]
    public void MultipleSeparatePermissionClaimsAreEvaluatedExactly()
    {
        var principal = Principal(
            Permission("ppm.projects.read"),
            Permission(RequiredPermission),
            Permission("ppm.programs.update"));

        Assert.True(_sut.HasPermission(principal, RequiredPermission));
        Assert.False(_sut.HasPermission(principal, "ppm.projects.create"));
    }

    [Fact]
    public void NonCanonicalPermissionClaimTypesDeny()
    {
        var principal = Principal(
            new Claim("permissions", RequiredPermission),
            new Claim("https://diten.com/permission", RequiredPermission));

        Assert.False(_sut.HasPermission(principal, RequiredPermission));
    }

    [Fact]
    public void EvaluatorHasNoRemoteServiceOrRepositoryDependencies()
    {
        var evaluatorType = typeof(SignedJwtPermissionClaimEvaluator);

        Assert.Single(evaluatorType.GetConstructors());
        Assert.Empty(evaluatorType.GetConstructors()[0].GetParameters());
        Assert.Empty(evaluatorType
            .GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic));
        Assert.All(
            typeof(IPermissionClaimEvaluator).GetMethods(),
            method => Assert.Equal(typeof(bool), method.ReturnType));
    }

    private static ClaimsPrincipal Principal(params Claim[] extraClaims)
        => Authenticated([Subject(), Tenant(), .. extraClaims]);

    private static ClaimsPrincipal PrincipalWithoutSubject(params Claim[] extraClaims)
        => Authenticated([Tenant(), .. extraClaims]);

    private static ClaimsPrincipal Authenticated(IEnumerable<Claim> claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

    private static Claim Subject()
        => new(SignedJwtPermissionClaimEvaluator.SubjectClaimType, Guid.NewGuid().ToString("D"));

    private static Claim Tenant()
        => new(SignedJwtPermissionClaimEvaluator.TenantClaimType, Guid.NewGuid().ToString("D"));

    private static Claim Permission(string value)
        => new(SignedJwtPermissionClaimEvaluator.PermissionClaimType, value);
}
