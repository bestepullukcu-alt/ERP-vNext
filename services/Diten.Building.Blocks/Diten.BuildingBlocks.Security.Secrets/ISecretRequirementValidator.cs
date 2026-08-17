namespace Diten.BuildingBlocks.Security.Secrets;

public interface ISecretRequirementValidator
{
    SecretValidationResult Validate(IEnumerable<RequiredSecretDefinition> requirements);
}
