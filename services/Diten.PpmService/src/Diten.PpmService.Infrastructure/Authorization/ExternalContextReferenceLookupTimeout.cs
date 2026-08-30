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


public sealed class ExternalContextReferenceLookupTimeout(
    IOptions<ExternalContextProviderOptions> options)
    : IExternalContextReferenceLookupTimeout
{
    public TimeSpan LookupTimeout =>
        TimeSpan.FromMilliseconds(options.Value.LookupTimeoutMilliseconds);
}
