namespace Diten.Platform.Application.Features.BusinessReferenceData;

/// <summary>
/// Marker for every BusinessReferenceData (PSS-012) MediatR command/query. Used by
/// <c>BusinessReferenceDataExceptionBehavior</c> to centrally map the module's coded domain
/// exceptions to <c>Response&lt;T&gt;.Fail(code, statusCode)</c> with the correct HTTP status,
/// so controllers stay thin and never perform business error mapping.
/// </summary>
public interface IBusinessReferenceDataRequest;
