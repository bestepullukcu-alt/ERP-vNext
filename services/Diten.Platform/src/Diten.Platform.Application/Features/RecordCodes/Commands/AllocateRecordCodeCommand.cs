using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.RecordCodes.Commands;

/// <summary>
/// MOD-0040-FU02 — allocate one record code. Not <c>IAuditableCommand</c>: the audit record is written by the handler, in
/// the allocation's own transaction, and every refusal from step 4 on is recorded by the handler too (pack §21).
/// Everything here comes from the trusted executor: the token's tenant and user, the credential binding's closed-list
/// service name and record types, the parsed body (or why it was refused) and the exact-permission verdict. No tenant and
/// no service name is ever read from the body.
/// </summary>
public sealed record AllocateRecordCodeCommand(
    Guid TenantId,
    Guid OnBehalfOfUserId,
    string CallerService,
    IReadOnlySet<string> AllowedRecordTypes,
    AllocateRecordCodeRequest? Request,
    IReadOnlyList<string> RequestErrors,
    bool HasRequiredPermission,
    string CorrelationId)
    : IRequest<Response<RecordCodeAllocationResult>>;
