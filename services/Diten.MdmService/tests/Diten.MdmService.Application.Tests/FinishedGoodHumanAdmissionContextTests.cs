using System.Reflection;
using System.Security.Claims;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FinishedGoodHumanAdmissionContextTests
{
    private const string SubmitPermission = "mdm.finished-goods.submit";
    private static readonly Guid TenantId = Guid.Parse("91abcdef-0000-0000-0000-000000000091");
    private static readonly Guid SubjectId = Guid.Parse("92abcdef-0000-0000-0000-000000000092");

    [Theory]
    [InlineData("sub", false)]
    [InlineData("nameidentifier", false)]
    [InlineData("sub", true)]
    public void TryResolveSubmitter_CanonicalSubjectVariants_ReturnExactTenantAndSubject(
        string subjectTransport,
        bool includeBoth)
    {
        var claims = BaseClaimsWithoutSubject();
        if (subjectTransport == "sub" || includeBoth)
        {
            claims.Add(new Claim("sub", SubjectId.ToString("D")));
        }
        if (subjectTransport == "nameidentifier" || includeBoth)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, SubjectId.ToString("D")));
        }
        var context = CreateContext(claims);

        var resolved = context.TryResolveSubmitter(out var tenantId, out var subjectId);

        Assert.True(resolved);
        Assert.Equal(TenantId, tenantId);
        Assert.Equal(SubjectId, subjectId);
    }

    [Theory]
    [InlineData("permission", "mdm.finished-goods.submit")]
    [InlineData("permissions", "mdm.finished-goods.submit")]
    [InlineData("permission", "mdm.finished-goods.read,mdm.finished-goods.submit")]
    [InlineData("permissions", "mdm.finished-goods.read mdm.finished-goods.submit")]
    [InlineData("permissions", "mdm.finished-goods.read;mdm.finished-goods.submit")]
    [InlineData("permissions", "mdm.finished-goods.read, mdm.finished-goods.submit;other.permission")]
    public void TryResolveSubmitter_ExistingPermissionTransportsWithExactOrdinalValue_Succeed(
        string claimType,
        string claimValue)
    {
        var claims = BaseClaimsWithoutPermission();
        claims.Add(new Claim(claimType, claimValue));
        var context = CreateContext(claims);

        var resolved = context.TryResolveSubmitter(out var tenantId, out var subjectId);

        Assert.True(resolved);
        Assert.Equal(TenantId, tenantId);
        Assert.Equal(SubjectId, subjectId);
    }

    [Fact]
    public void TryResolveSubmitter_MissingHttpContextOrUnauthenticatedPrincipal_DeniesWithoutPartialIdentity()
    {
        AssertDenied(CreateContext(ValidClaims(), includeHttpContext: false));
        AssertDenied(CreateContext(ValidClaims(), authenticated: false));
    }

    [Fact]
    public void TryResolveSubmitter_MissingDuplicateOrNonTenantUserActor_DeniesWithoutPartialIdentity()
    {
        var actorCases = new[]
        {
            Array.Empty<string>(),
            new[] { "tenant_user", "tenant_user" },
            new[] { "service" },
            new[] { "platform_admin" },
            new[] { "partner_admin" },
            new[] { "Tenant_User" },
            new[] { string.Empty }
        };

        foreach (var actorValues in actorCases)
        {
            var claims = ValidClaims();
            ReplaceClaims(claims, "actor_type", actorValues);
            AssertDenied(CreateContext(claims));
        }
    }

    [Fact]
    public void TryResolveSubmitter_InvalidSubjectShapes_DenyWithoutPartialIdentity()
    {
        var otherSubject = Guid.Parse("93000000-0000-0000-0000-000000000093");
        var subjectCases = new[]
        {
            Array.Empty<Claim>(),
            new[] { new Claim("sub", SubjectId.ToString("D")), new Claim("sub", SubjectId.ToString("D")) },
            new[] { new Claim(ClaimTypes.NameIdentifier, SubjectId.ToString("D")), new Claim(ClaimTypes.NameIdentifier, SubjectId.ToString("D")) },
            new[] { new Claim("sub", SubjectId.ToString("D")), new Claim(ClaimTypes.NameIdentifier, otherSubject.ToString("D")) },
            new[] { new Claim("sub", "not-a-guid") },
            new[] { new Claim("sub", Guid.Empty.ToString("D")) },
            new[] { new Claim("sub", SubjectId.ToString("D").ToUpperInvariant()) },
            new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") },
            new[] { new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString("D")) },
            new[] { new Claim(ClaimTypes.NameIdentifier, string.Empty) }
        };

        foreach (var subjectClaims in subjectCases)
        {
            var claims = BaseClaimsWithoutSubject();
            claims.AddRange(subjectClaims);
            AssertDenied(CreateContext(claims));
        }
    }

    [Fact]
    public void TryResolveSubmitter_InvalidTenantClaimOrResolvedContext_DeniesWithoutPartialIdentity()
    {
        var otherTenant = Guid.Parse("94000000-0000-0000-0000-000000000094");
        var tenantCases = new[]
        {
            Array.Empty<string>(),
            new[] { TenantId.ToString("D"), TenantId.ToString("D") },
            new[] { "not-a-guid" },
            new[] { Guid.Empty.ToString("D") },
            new[] { string.Empty },
            new[] { TenantId.ToString("D").ToUpperInvariant() }
        };

        foreach (var tenantValues in tenantCases)
        {
            var claims = ValidClaims();
            ReplaceClaims(claims, "tenant_id", tenantValues);
            AssertDenied(CreateContext(claims));
        }

        AssertDenied(CreateContext(ValidClaims(), tenantContext: new TestTenantContext(otherTenant, true)));
        AssertDenied(CreateContext(ValidClaims(), tenantContext: new TestTenantContext(TenantId, false)));
        AssertDenied(CreateContext(ValidClaims(), tenantContext: new TestTenantContext(Guid.Empty, true)));

        var headerOnlyClaims = ValidClaims();
        ReplaceClaims(headerOnlyClaims, "tenant_id", []);
        AssertDenied(CreateContext(headerOnlyClaims, [TenantId.ToString("D")]));
    }

    [Fact]
    public void TryResolveSubmitter_HeaderAbsentOrSingleCanonicalMatch_Succeeds()
    {
        var withoutHeader = CreateContext(ValidClaims());
        var withHeader = CreateContext(ValidClaims(), [TenantId.ToString("D")]);

        Assert.True(withoutHeader.TryResolveSubmitter(out var tenantWithoutHeader, out var subjectWithoutHeader));
        Assert.Equal(TenantId, tenantWithoutHeader);
        Assert.Equal(SubjectId, subjectWithoutHeader);
        Assert.True(withHeader.TryResolveSubmitter(out var tenantWithHeader, out var subjectWithHeader));
        Assert.Equal(TenantId, tenantWithHeader);
        Assert.Equal(SubjectId, subjectWithHeader);
    }

    [Fact]
    public void TryResolveSubmitter_InvalidHeaderShapes_DenyWithoutPartialIdentity()
    {
        var otherTenant = Guid.Parse("95000000-0000-0000-0000-000000000095");
        var headerCases = new[]
        {
            new[] { string.Empty },
            new[] { TenantId.ToString("D"), TenantId.ToString("D") },
            new[] { $"{TenantId:D},{TenantId:D}" },
            new[] { "not-a-guid" },
            new[] { Guid.Empty.ToString("D") },
            new[] { TenantId.ToString("D").ToUpperInvariant() },
            new[] { otherTenant.ToString("D") }
        };

        foreach (var headerValues in headerCases)
        {
            AssertDenied(CreateContext(ValidClaims(), headerValues));
        }
    }

    [Fact]
    public void TryResolveSubmitter_MissingOrSubstitutePermission_DeniesWithoutPartialIdentity()
    {
        var permissionCases = new[]
        {
            Array.Empty<string>(),
            new[] { "mdm.finished-goods.read" },
            new[] { "mdm.finished-goods.create" },
            new[] { "mdm.finished-goods.*" },
            new[] { "mdm.finished-goods.submit-extra" },
            new[] { "MDM.FINISHED-GOODS.SUBMIT" },
            new[] { "mdm.finished-goods.submi" },
            new[] { string.Empty }
        };

        foreach (var permissionValues in permissionCases)
        {
            var claims = BaseClaimsWithoutPermission();
            foreach (var permissionValue in permissionValues)
            {
                claims.Add(new Claim("permissions", permissionValue));
            }
            AssertDenied(CreateContext(claims));
        }
    }

    [Fact]
    public void ContextContract_HasOnlyLocalHttpAndTenantDependenciesAndNoSideEffectSurface()
    {
        var contractMethod = Assert.Single(typeof(IFinishedGoodHumanAdmissionContext).GetMethods());
        Assert.Equal(nameof(IFinishedGoodHumanAdmissionContext.TryResolveSubmitter), contractMethod.Name);
        Assert.Equal(typeof(bool), contractMethod.ReturnType);
        Assert.All(contractMethod.GetParameters(), parameter =>
        {
            Assert.True(parameter.IsOut);
            Assert.Equal(typeof(Guid).MakeByRefType(), parameter.ParameterType);
        });

        var constructor = Assert.Single(typeof(FinishedGoodHumanAdmissionContext).GetConstructors());
        Assert.Collection(
            constructor.GetParameters(),
            parameter => Assert.Equal(typeof(IHttpContextAccessor), parameter.ParameterType),
            parameter => Assert.Equal(typeof(ITenantContext), parameter.ParameterType));

        var instanceFieldTypes = typeof(FinishedGoodHumanAdmissionContext)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.Equal([typeof(IHttpContextAccessor), typeof(ITenantContext)], instanceFieldTypes);
    }

    private static FinishedGoodHumanAdmissionContext CreateContext(
        IReadOnlyCollection<Claim> claims,
        IReadOnlyCollection<string>? tenantHeaderValues = null,
        bool authenticated = true,
        bool includeHttpContext = true,
        ITenantContext? tenantContext = null)
    {
        var httpContext = includeHttpContext ? new DefaultHttpContext() : null;
        if (httpContext is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                authenticated ? "Bearer" : null));
            if (tenantHeaderValues is not null)
            {
                httpContext.Request.Headers["X-Tenant-Id"] = new StringValues(tenantHeaderValues.ToArray());
            }
        }

        return new FinishedGoodHumanAdmissionContext(
            new HttpContextAccessor { HttpContext = httpContext },
            tenantContext ?? new TestTenantContext(TenantId, true));
    }

    private static List<Claim> ValidClaims() =>
    [
        new("actor_type", "tenant_user"),
        new("sub", SubjectId.ToString("D")),
        new("tenant_id", TenantId.ToString("D")),
        new("permissions", SubmitPermission)
    ];

    private static List<Claim> BaseClaimsWithoutSubject() =>
    [
        new("actor_type", "tenant_user"),
        new("tenant_id", TenantId.ToString("D")),
        new("permissions", SubmitPermission)
    ];

    private static List<Claim> BaseClaimsWithoutPermission() =>
    [
        new("actor_type", "tenant_user"),
        new("sub", SubjectId.ToString("D")),
        new("tenant_id", TenantId.ToString("D"))
    ];

    private static void ReplaceClaims(List<Claim> claims, string claimType, IEnumerable<string> values)
    {
        claims.RemoveAll(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal));
        claims.AddRange(values.Select(value => new Claim(claimType, value)));
    }

    private static void AssertDenied(FinishedGoodHumanAdmissionContext context)
    {
        var resolved = context.TryResolveSubmitter(out var tenantId, out var subjectId);

        Assert.False(resolved);
        Assert.Equal(Guid.Empty, tenantId);
        Assert.Equal(Guid.Empty, subjectId);
    }

    private sealed class TestTenantContext(Guid tenantId, bool isResolved) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved { get; private set; } = isResolved;

        public void SetTenant(Guid tenantId)
        {
            TenantId = tenantId;
            IsResolved = tenantId != Guid.Empty;
        }
    }
}
