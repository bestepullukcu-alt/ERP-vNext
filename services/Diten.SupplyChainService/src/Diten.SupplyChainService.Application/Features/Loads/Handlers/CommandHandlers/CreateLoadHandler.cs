using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Loads;
using Diten.SupplyChainService.Application.Features.Loads.Commands;
namespace Diten.SupplyChainService.Application.Features.Loads.Handlers.CommandHandlers;
public sealed class CreateLoadHandler(ILoadRepository repository,LoadRequestContext context,ILoadReferenceReader reader) : IRequestHandler<CreateLoadCommand,Response<LoadResponse>>
{ public async Task<Response<LoadResponse>> Handle(CreateLoadCommand request,CancellationToken ct)
 { var r=await repository.MutateAsync(context.Scope,null,context.IdempotencyKey,LoadRequestFingerprint.Create(request.Body),context.CorrelationId,LoadWire.Plan(request.Body), null, null, null,reader.ObserveAsync,ct);
 return r.ErrorCode is null?Response<LoadResponse>.Success(new(r.LoadId,r.LoadNumber,r.Status,r.IdempotentReplay),r.StatusCode):Response<LoadResponse>.Fail(r.ErrorCode,r.StatusCode); } }
