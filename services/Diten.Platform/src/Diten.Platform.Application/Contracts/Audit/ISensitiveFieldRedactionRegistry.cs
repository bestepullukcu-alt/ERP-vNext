namespace Diten.Platform.Application.Contracts.Audit;

public interface ISensitiveFieldRedactionRegistry
{
    IReadOnlyList<SensitiveFieldRule> GetRules();
}
