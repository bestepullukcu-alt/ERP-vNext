using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;


public interface IExternalContextReferenceLookup
{
    Task<ExternalContextReferenceLookupResult?> FindAsync(
        Guid tenantId,
        string contextKind,
        Guid contextId,
        CancellationToken cancellationToken);
}
