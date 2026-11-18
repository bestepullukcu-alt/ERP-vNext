using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>listWaveSuggestions</c> (D-1 "Suggest") — reads only, writes nothing.</summary>
public sealed record GetWaveSuggestionListQuery(Guid WaveId) : IRequest<Response<WaveSuggestionsDto>>;
