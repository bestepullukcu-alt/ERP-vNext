using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record BusinessReferenceDataVersionValueInputModel(
    string Code,
    string Label,
    string? Description,
    bool IsActive,
    int SortOrder,
    string? ParentValueCode,
    IReadOnlyDictionary<string, string>? Attributes);
