using Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking;using Xunit;
public class SecurityTests{[Fact]public void Permissions_have_no_wildcards()=>Assert.All(AuthorizationManifest.Operations.Values,x=>Assert.DoesNotContain("*",x));}
