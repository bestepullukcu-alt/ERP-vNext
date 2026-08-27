using System.Reflection;
using Diten.FpaService.Api.Modules.ScenarioPlanning;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authorization;

namespace Diten.FpaService.ArchitectureTests.Modules.ScenarioPlanning.GateI;

public sealed class Fu16ScenarioGateICompositionArchitectureTests
{
 [Fact]public void Controller_uses_exact_named_scheme(){var attribute=typeof(InternalScenarioReferenceValidationController).GetCustomAttribute<AuthorizeAttribute>();Assert.NotNull(attribute);Assert.Equal(Fu16AuthenticationOptions.Scheme,attribute.AuthenticationSchemes);}
 [Fact]public void Evidence_registration_does_not_activate_authority_or_host(){var root=FindRoot();var text=File.ReadAllText(Path.Combine(root,"src/Diten.FpaService.Api/Modules/ScenarioPlanning/ScenarioPlanningModuleRegistration.cs"));Assert.DoesNotContain("AddHostedService",text,StringComparison.Ordinal);Assert.DoesNotContain("AddSingleton<IFu16ValidationKeyProvider",text,StringComparison.Ordinal);Assert.DoesNotContain("AddSingleton<IFu16AuthorityDecisionClient",text,StringComparison.Ordinal);}
 private static string FindRoot(){var path=new DirectoryInfo(AppContext.BaseDirectory);while(path is not null&&!Directory.Exists(Path.Combine(path.FullName,"src")))path=path.Parent;return path?.FullName??throw new InvalidOperationException("Service root not found.");}
}
