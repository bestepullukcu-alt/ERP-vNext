using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;


public sealed class ValidateExternalContextReferenceHandler(
    ITenantContext tenant,
    IPpmAccessAuthorizer authorizer,
    IExternalContextReferenceLookup lookup,
    IExternalContextReferenceLookupTimeout lookupTimeout)
    : IRequestHandler<ValidateExternalContextReferenceQuery, Response<ExternalContextReferenceResponse>>
{
    public async Task<Response<ExternalContextReferenceResponse>> Handle(
        ValidateExternalContextReferenceQuery request,
        CancellationToken cancellationToken)
    {
        var permission = ExternalContextReferenceContract.PermissionFor(request.ContextKind!);
        if (permission is null ||
            !Guid.TryParseExact(request.ContextId, "D", out var contextId) ||
            contextId == Guid.Empty)
        {
            return Response<ExternalContextReferenceResponse>.Fail("Invalid external context reference.", 400);
        }

        var access = await authorizer.AuthorizeAsync(permission, cancellationToken);
        if (access != PpmAccessDecision.Allowed)
        {
            return access.Failure<ExternalContextReferenceResponse>();
        }

        using var lookupCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lookupCancellation.CancelAfter(lookupTimeout.LookupTimeout);

        try
        {
            var candidate = await lookup.FindAsync(
                tenant.TenantId,
                request.ContextKind!,
                contextId,
                lookupCancellation.Token);

            if (candidate is null || !candidate.IsReferenceable || candidate.VisibilityPolicyKey is not null)
            {
                return Response<ExternalContextReferenceResponse>.Fail("External context was not found.", 404);
            }

            return Response<ExternalContextReferenceResponse>.Success(new(
                ExternalContextReferenceContract.Name,
                ExternalContextReferenceContract.Version,
                request.ContextKind!,
                contextId.ToString("D")));
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested &&
                  lookupCancellation.IsCancellationRequested)
        {
            return Response<ExternalContextReferenceResponse>.Fail(
                "Authoritative external context lookup is unavailable.",
                503);
        }
        catch (ExternalContextReferenceDependencyException)
        {
            return Response<ExternalContextReferenceResponse>.Fail(
                "Authoritative external context lookup is unavailable.",
                503);
        }
    }
}
