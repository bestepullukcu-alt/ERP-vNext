using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers;

// MOD-0029-FU06 — thin MediatR handlers delegating to DocumentMasterRegisterService.

public sealed class CreateMasterRegisterEntryHandler(DocumentMasterRegisterService service)
    : IRequestHandler<CreateMasterRegisterEntryCommand, Response<MasterRegisterDetailModel>>
{
    public Task<Response<MasterRegisterDetailModel>> Handle(CreateMasterRegisterEntryCommand request, CancellationToken ct) =>
        service.CreateAsync(request.Input, request.CorrelationId, ct);
}

public sealed class UpdateMasterRegisterMetadataHandler(DocumentMasterRegisterService service)
    : IRequestHandler<UpdateMasterRegisterMetadataCommand, Response<MasterRegisterDetailModel>>
{
    public Task<Response<MasterRegisterDetailModel>> Handle(UpdateMasterRegisterMetadataCommand request, CancellationToken ct) =>
        service.UpdateMetadataAsync(request.EntryId, request.Input, request.CorrelationId, ct);
}

public sealed class LinkControlledDocumentToRegisterEntryHandler(DocumentMasterRegisterService service)
    : IRequestHandler<LinkControlledDocumentToRegisterEntryCommand, Response<MasterRegisterDetailModel>>
{
    public Task<Response<MasterRegisterDetailModel>> Handle(LinkControlledDocumentToRegisterEntryCommand request, CancellationToken ct) =>
        service.LinkControlledDocumentAsync(
            request.EntryId,
            request.Input.ControlledDocumentId,
            request.Input.ReconciliationReason,
            request.CorrelationId,
            ct);
}

public sealed class GetMasterRegisterListHandler(DocumentMasterRegisterService service)
    : IRequestHandler<GetMasterRegisterListQuery, Response<IReadOnlyList<MasterRegisterListItemModel>>>
{
    public Task<Response<IReadOnlyList<MasterRegisterListItemModel>>> Handle(GetMasterRegisterListQuery request, CancellationToken ct) =>
        service.ListAsync(
            MasterRegisterWire.ToFilter(request.RegisterStatus, request.LifecycleStatus, request.Criticality, request.DocumentClass, request.OwnerCompanyId),
            request.CorrelationId, ct);
}

public sealed class GetMasterRegisterEntryByIdHandler(DocumentMasterRegisterService service)
    : IRequestHandler<GetMasterRegisterEntryByIdQuery, Response<MasterRegisterDetailModel>>
{
    public Task<Response<MasterRegisterDetailModel>> Handle(GetMasterRegisterEntryByIdQuery request, CancellationToken ct) =>
        service.GetDetailAsync(request.EntryId, request.CorrelationId, ct);
}

public sealed class GetMasterRegisterSummaryHandler(DocumentMasterRegisterService service)
    : IRequestHandler<GetMasterRegisterSummaryQuery, Response<MasterRegisterSummaryModel>>
{
    public Task<Response<MasterRegisterSummaryModel>> Handle(GetMasterRegisterSummaryQuery request, CancellationToken ct) =>
        service.GetSummaryAsync(request.CorrelationId, ct);
}
