using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>Puts a draft rule live and FREEZES its criteria. Separate endpoint and separate permission from
/// <c>manage</c>, so whoever writes a rule need not be whoever activates it. Freezing is what makes a past resolution
/// explainable: a result can only be justified by a (SegmentId, SegmentVersion) pair if that pair still asks the same
/// question it asked then.</summary>
public sealed record ActivateSegmentCommand(Guid SegmentId, int? ExpectedVersion) : IRequest<Response<bool>>;
