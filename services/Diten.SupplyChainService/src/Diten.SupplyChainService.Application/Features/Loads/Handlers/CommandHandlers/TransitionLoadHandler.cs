using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Loads;
using Diten.SupplyChainService.Application.Features.Loads.Commands;
namespace Diten.SupplyChainService.Application.Features.Loads.Handlers.CommandHandlers;
public sealed class TransitionLoadHandler(ILoadRepository repository,LoadRequestContext context,ILoadReferenceReader reader) : IRequestHandler<TransitionLoadCommand,Response<LoadResponse>>
{ public async Task<Response<LoadResponse>> Handle(TransitionLoadCommand request,CancellationToken ct)
 { var r=await repository.MutateAsync(context.Scope,request.LoadId,context.IdempotencyKey,LoadRequestFingerprint.Transition(request.Body),context.CorrelationId,null, Enum.Parse<LoadStatus>(request.Body.GetProperty("targetStatus").GetString()!), request.Body.GetProperty("occurredAt").GetString(), request.Body.TryGetProperty("note",out var note)?note.GetString():null,reader.ObserveAsync,ct);
 return r.ErrorCode is null?Response<LoadResponse>.Success(new(r.LoadId,r.LoadNumber,r.Status,r.IdempotentReplay),r.StatusCode):Response<LoadResponse>.Fail(r.ErrorCode,r.StatusCode); } }
