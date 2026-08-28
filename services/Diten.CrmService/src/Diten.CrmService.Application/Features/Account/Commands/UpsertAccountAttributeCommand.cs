using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Commands;

public sealed record UpsertAccountAttributeCommand(Guid AccountId, string AttributeCode, string? Value) : IRequest<Response<bool>>;
