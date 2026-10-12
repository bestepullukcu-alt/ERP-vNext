namespace Diten.Web.Security;

/// <summary>
/// Marks same-origin MVC adapter endpoints whose authentication challenge is JSON.
/// The marker changes only the cookie challenge representation; authorization still
/// runs through the normal ASP.NET Core pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class JsonAdapterEndpointAttribute : Attribute;
