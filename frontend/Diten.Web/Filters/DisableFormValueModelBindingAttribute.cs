using System;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Diten.Web.Filters;

/// <summary>
/// WP-VW-W3 (CT, live E4 2026-10-12) — for a proxy action that forwards the browser's multipart body AS-IS.
/// <para>
/// MVC builds its value providers before the action runs whenever the action has any parameter to bind (even a lone
/// <see cref="System.Threading.CancellationToken"/>), and for a multipart request the form value provider reads the
/// whole form — which consumes <c>Request.Body</c>. The proxy then copies an EMPTY stream: live this surfaced as 502
/// ("Gateway unavailable": Content-Length promised, 0 bytes sent) and, once buffered, as a CRM 404 (no
/// <c>plannedVisitId</c> in an empty form). Unit tests with an in-memory body could not see it.
/// </para>
/// <para>This resource filter removes the form and form-file value provider factories for the decorated action only,
/// so the body is still unread when the action copies it (the pattern from the ASP.NET Core large-upload docs).</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
