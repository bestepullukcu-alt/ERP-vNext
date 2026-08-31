using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;


public static class ExternalContextReferenceContract
{
    public const string Name = "ppm.external-context-reference";
    public const string Version = "1.0";

    public static string? PermissionFor(string contextKind) => contextKind switch
    {
        "Portfolio" => PpmPermissions.PortfoliosRead,
        "Initiative" => PpmPermissions.InitiativesRead,
        "Program" => PpmPermissions.ProgramsRead,
        "Project" => PpmPermissions.ProjectsRead,
        _ => null
    };
}
