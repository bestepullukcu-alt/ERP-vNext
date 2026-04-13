using Diten.Application.Commands.DemandIdeaCommands;
using Diten.Application.Common.Interfaces;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Domain.Aggregates.DemandIdea;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.CommandHandlers;

public sealed class SubmitDemandIdeaCommandHandler : IRequestHandler<SubmitDemandIdeaCommand, Response<DemandIdeaResponseDto>>
{
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public SubmitDemandIdeaCommandHandler(IRepository<DemandIdeaAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<Response<DemandIdeaResponseDto>> Handle(SubmitDemandIdeaCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id);
        if (entity == null || entity.IsDeleted)
        {
            return Response<DemandIdeaResponseDto>.Fail(ResultErrorCodes.NotFound);
        }

        if (!DemandIdeaHandlerSupport.CanSubmit(entity))
        {
            return Response<DemandIdeaResponseDto>.Fail(
                ResultErrorCodes.Conflict,
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Status"] = new() { "Record is not in Draft status." }
                });
        }

        var validationErrors = DemandIdeaHandlerSupport.ValidateSubmit(entity);
        if (validationErrors.Count > 0)
        {
            return Response<DemandIdeaResponseDto>.Fail(ResultErrorCodes.Validation, validationErrors);
        }

        entity.Status = "Submitted";
        entity.ReviewDueDate = DateTime.UtcNow.Date.AddDays(14);
        entity.LastModifiedDate = DateTime.UtcNow;
        entity.LastModifiedBy = request.UserId;

        await _repository.UpdateAsync(entity);
        return Response<DemandIdeaResponseDto>.Ok(DemandIdeaHandlerSupport.MapToDto(entity));
    }
}
