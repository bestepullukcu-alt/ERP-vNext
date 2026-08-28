using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Commands;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Queries;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementIdentifiers.Handlers;

// MOD-0029-FU07 — thin MediatR handlers delegating to DocumentIdentifierAllocationService.

public sealed class AllocateUidHandler(DocumentIdentifierAllocationService service)
    : IRequestHandler<AllocateUidCommand, Response<IdentifierAllocationResultModel>>
{
    public Task<Response<IdentifierAllocationResultModel>> Handle(AllocateUidCommand request, CancellationToken ct) =>
        service.AllocateUidAsync(request.RegisterEntryId, request.Input.AllocationReason, request.CorrelationId, ct);
}

public sealed class AllocateCodeHandler(DocumentIdentifierAllocationService service)
    : IRequestHandler<AllocateCodeCommand, Response<IdentifierAllocationResultModel>>
{
    public Task<Response<IdentifierAllocationResultModel>> Handle(AllocateCodeCommand request, CancellationToken ct) =>
        service.AllocateCodeAsync(request.RegisterEntryId, request.Input.AllocationReason, request.CorrelationId, ct);
}

public sealed class AllocateIdentifiersHandler(DocumentIdentifierAllocationService service)
    : IRequestHandler<AllocateIdentifiersCommand, Response<IdentifierAllocationResultModel>>
{
    public Task<Response<IdentifierAllocationResultModel>> Handle(AllocateIdentifiersCommand request, CancellationToken ct) =>
        service.AllocateIdentifiersAsync(request.RegisterEntryId, request.Input.AllocationReason, request.CorrelationId, ct);
}

public sealed class ReserveIdentifierHandler(DocumentIdentifierAllocationService service)
    : IRequestHandler<ReserveIdentifierCommand, Response<IdentifierAllocationModel>>
{
    public Task<Response<IdentifierAllocationModel>> Handle(ReserveIdentifierCommand request, CancellationToken ct) =>
        service.ReserveAsync(request.Input, request.CorrelationId, ct);
}

public sealed class CancelIdentifierHandler(DocumentIdentifierAllocationService service)
    : IRequestHandler<CancelIdentifierCommand, Response<IdentifierAllocationModel>>
{
    public Task<Response<IdentifierAllocationModel>> Handle(CancelIdentifierCommand request, CancellationToken ct) =>
        service.CancelAsync(request.AllocationId, request.Input, request.CorrelationId, ct);
}

public sealed class GetIdentifierAllocationsHandler(DocumentIdentifierAllocationService service)
    : IRequestHandler<GetIdentifierAllocationsQuery, Response<IReadOnlyList<IdentifierAllocationModel>>>
{
    public Task<Response<IReadOnlyList<IdentifierAllocationModel>>> Handle(GetIdentifierAllocationsQuery request, CancellationToken ct) =>
        service.ListAsync(
            IdentifierWire.ToFilter(request.IdentifierType, request.AllocationStatus, request.RegisterEntryId),
            request.CorrelationId, ct);
}
