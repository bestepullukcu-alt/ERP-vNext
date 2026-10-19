using System.Reflection;
using Diten.Web.Controllers.CRM;
using Diten.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3 (CT, live E4 2026-10-12) — the multipart proxies (start visit, signature) forward the browser's body
/// as-is. MVC's form value provider would read (consume) that body before the action whenever the action binds any
/// parameter; live this sent an EMPTY body (502, then a CRM 404 with no plannedVisitId). The actions therefore carry
/// <see cref="DisableFormValueModelBindingAttribute"/>, and the attribute must actually remove the form providers.
/// </summary>
public sealed class MultipartProxyBodyWebTests
{
    [Theory]
    [InlineData(typeof(VisitWorkspaceController), nameof(VisitWorkspaceController.StartVisit))]
    [InlineData(typeof(VisitPresentationController), nameof(VisitPresentationController.Signature))]
    public void The_multipart_proxy_actions_keep_the_body_unread(Type controller, string action)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<DisableFormValueModelBindingAttribute>());
    }

    [Fact]
    public void The_attribute_removes_every_form_value_provider_and_keeps_the_others()
    {
        var factories = new List<IValueProviderFactory>
        {
            new FormValueProviderFactory(),
            new FormFileValueProviderFactory(),
            new JQueryFormValueProviderFactory(),
            new RouteValueProviderFactory(),
            new QueryStringValueProviderFactory()
        };
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ResourceExecutingContext(actionContext, new List<IFilterMetadata>(), factories);

        new DisableFormValueModelBindingAttribute().OnResourceExecuting(context);

        Assert.DoesNotContain(context.ValueProviderFactories, f => f is FormValueProviderFactory);
        Assert.DoesNotContain(context.ValueProviderFactories, f => f is FormFileValueProviderFactory);
        Assert.DoesNotContain(context.ValueProviderFactories, f => f is JQueryFormValueProviderFactory);
        Assert.Contains(context.ValueProviderFactories, f => f is RouteValueProviderFactory); // the route id still binds
        Assert.Contains(context.ValueProviderFactories, f => f is QueryStringValueProviderFactory);
    }
}
