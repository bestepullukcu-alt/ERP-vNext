using System.Security.Claims;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Application.Tests.Users;

public sealed class UserLookupValidationEndpointTests
{
    [Fact]
    public void UsersControllerHasAuthorizeAttribute()
    {
        var authorize = typeof(UsersController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

        Assert.NotEmpty(authorize);
    }

    [Fact]
    public void LookupValidationEndpointHasPermissionAttribute()
    {
        var method = GetLookupValidationMethod();

        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());

        Assert.Equal("Permission:auth.users.lookup-validation", attribute.Policy);
    }

    [Fact]
    public void LookupValidationEndpointRouteIsLocked()
    {
        var controllerRoute = Assert.Single(typeof(UsersController).GetCustomAttributes<RouteAttribute>());
        var method = GetLookupValidationMethod();
        var route = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());

        Assert.Equal("api/users", controllerRoute.Template);
        Assert.Equal("{userId:guid}/lookup-validation", route.Template);
    }

    [Fact]
    public async Task LookupValidationEndpointReturnsBadRequestWhenJwtAndHeaderTenantsMismatch()
    {
        var jwtTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var headerTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", jwtTenantId.ToString())]))
        };
        httpContext.Request.Headers["X-Tenant-Id"] = headerTenantId.ToString();
        var controller = new UsersController(new ThrowingMediator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        var result = await controller.ValidateLookupReference(Guid.NewGuid(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public void LookupValidationTenantMismatchGuardDoesNotRequireGlobalMiddlewareChange()
    {
        var middlewarePath = Path.Combine(
            "src",
            "Diten.AuthService.Infrastructure",
            "Middleware",
            "TenantResolutionMiddleware.cs");

        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, middlewarePath)));
        Assert.True(typeof(UsersController).GetMethod(nameof(UsersController.HasTenantHeaderJwtMismatch)) is not null);
    }

    private static System.Reflection.MethodInfo GetLookupValidationMethod()
    {
        return typeof(UsersController).GetMethod(nameof(UsersController.ValidateLookupReference))
            ?? throw new InvalidOperationException("Lookup-validation endpoint is missing.");
    }

    private sealed class ThrowingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Mediator should not be called when tenant mismatch guard fails.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Mediator should not be called when tenant mismatch guard fails.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw new InvalidOperationException("Mediator should not be called when tenant mismatch guard fails.");
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Streaming is not used by this controller.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Streaming is not used by this controller.");
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Publishing is not used by this controller.");
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            throw new InvalidOperationException("Publishing is not used by this controller.");
        }
    }
}
