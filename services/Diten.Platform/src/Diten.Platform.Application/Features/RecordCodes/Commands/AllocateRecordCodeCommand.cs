using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.RecordCodes.Commands;

/// <summary>
/// MOD-0040-FU02 — allocate one record code. Not <c>IAuditableCommand</c>: the audit record is written by the handler, in
/// the allocation's own transaction (pack §21). <see cref="TenantId"/> and <see cref="CallerService"/> come from the trusted
/// executor (token tenant, credential binding), never from the body.
/// </summary>
public sealed record AllocateRecordCodeCommand(Guid TenantId, string CallerService)
    : IRequest<Response<RecordCodeAllocationResult>>;
