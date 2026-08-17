using Diten.Application.Commands.DemandIdeaCommands;
using Diten.Application.Common.Interfaces;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Domain.Aggregates.DemandIdea;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.CommandHandlers;

public sealed class CreateDemandIdeaDraftCommandHandler : IRequestHandler<CreateDemandIdeaDraftCommand, Response<DemandIdeaResponseDto>>
{
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public CreateDemandIdeaDraftCommandHandler(IRepository<DemandIdeaAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<Response<DemandIdeaResponseDto>> Handle(CreateDemandIdeaDraftCommand request, CancellationToken cancellationToken)
    {
        var all = await _repository.GetAllAsync();
        var entity = new DemandIdeaAggregate
        {
            RecordNumber = DemandIdeaHandlerSupport.NextRecordNumber(all),
            Status = "Draft",
            CreatedBy = request.UserId,
            LastModifiedBy = request.UserId,
            ReviewDueDate = DateTime.UtcNow.Date.AddDays(14)
        };

        DemandIdeaHandlerSupport.ApplyUpsert(entity, request.Request);
        if (string.IsNullOrWhiteSpace(entity.OwnerName) && !string.IsNullOrWhiteSpace(entity.Requestor))
        {
            entity.OwnerName = entity.Requestor;
        }

        await _repository.AddAsync(entity);
        return Response<DemandIdeaResponseDto>.Ok(DemandIdeaHandlerSupport.MapToDto(entity));
    }
}
