using Diten.Application.Commands.DemandIdeaCommands;
using Diten.Application.Common.Interfaces;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Domain.Aggregates.DemandIdea;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.CommandHandlers;

public sealed class UpdateDemandIdeaCommandHandler : IRequestHandler<UpdateDemandIdeaCommand, Response<DemandIdeaResponseDto>>
{
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public UpdateDemandIdeaCommandHandler(IRepository<DemandIdeaAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<Response<DemandIdeaResponseDto>> Handle(UpdateDemandIdeaCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id);
        if (entity == null || entity.IsDeleted)
        {
            return Response<DemandIdeaResponseDto>.Fail(ResultErrorCodes.NotFound);
        }

        if (!DemandIdeaHandlerSupport.CanUpdate(entity))
        {
            return Response<DemandIdeaResponseDto>.Fail(
                ResultErrorCodes.Conflict,
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Status"] = new() { "Only Draft records can be updated." }
                });
        }

        DemandIdeaHandlerSupport.ApplyUpsert(entity, request.Request);
        entity.LastModifiedDate = DateTime.UtcNow;
        entity.LastModifiedBy = request.UserId;
        if (string.IsNullOrWhiteSpace(entity.OwnerName) && !string.IsNullOrWhiteSpace(entity.Requestor))
        {
            entity.OwnerName = entity.Requestor;
        }

        await _repository.UpdateAsync(entity);
        return Response<DemandIdeaResponseDto>.Ok(DemandIdeaHandlerSupport.MapToDto(entity));
    }
}
