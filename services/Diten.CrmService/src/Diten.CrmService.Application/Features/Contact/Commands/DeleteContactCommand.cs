using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Contact.Commands;

public sealed record DeleteContactCommand(Guid Id) : IRequest<Response<bool>>;
