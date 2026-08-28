using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Commands;

/// <summary>
/// Puts a play live: draft to active, stamping <c>BindingsFrozenAt</c> so the bindings stop moving under the field.
/// Every bound segment must be ACTIVE at this moment (409 otherwise) — activating a play whose population is still a
/// draft would promise the field something that does not exist yet.
/// <para>Its own canonical permission (<c>crm.strategy-template.activate</c>) exists so the author need not be the
/// activator; under the documented dev fallback that separation collapses onto manage (F-RBAC).</para>
/// </summary>
public sealed record ActivateStrategyTemplateCommand(
    Guid TemplateId,
    int? ExpectedVersion) : IRequest<Response<bool>>;
