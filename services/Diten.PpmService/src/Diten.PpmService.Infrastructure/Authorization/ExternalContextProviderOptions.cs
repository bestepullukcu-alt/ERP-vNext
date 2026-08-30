using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.Features.ExternalContextReferences;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Authorization;


public sealed class ExternalContextProviderOptions
{
    public const string SectionName = "ExternalContextProvider";
    public const string CredentialHeader = "X-PPM-External-Context-Key";
    public const string ConsumerHeader = "X-PPM-Consumer";
    public const string AllowedConsumer = "Diten.ManagementGovernanceService";

    public bool Enabled { get; init; }
    public string? ServiceCredential { get; init; }
    public int LookupTimeoutMilliseconds { get; init; } = 2000;
}
