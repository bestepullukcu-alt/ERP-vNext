namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

public static class ProcessModelingPermissions
{
    public const string ModuleCode = "MOD-0355";
    public static IReadOnlyList<string> ExactPermissions { get; } =
    [
        "management-governance.process-modeling.architectures.read", "management-governance.process-modeling.architectures.create",
        "management-governance.process-modeling.architectures.update", "management-governance.process-modeling.architectures.archive",
        "management-governance.process-modeling.definitions.read", "management-governance.process-modeling.definitions.create",
        "management-governance.process-modeling.definitions.update", "management-governance.process-modeling.definitions.archive",
        "management-governance.process-modeling.models.read", "management-governance.process-modeling.models.create",
        "management-governance.process-modeling.models.update", "management-governance.process-modeling.models.request-review",
        "management-governance.process-modeling.models.return-to-draft", "management-governance.process-modeling.models.publish",
        "management-governance.process-modeling.models.retire", "management-governance.process-modeling.models.create-revision"
    ];

    public static IReadOnlyDictionary<string, string> ExactCommandMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CreateProcessArchitectureCommand"] = ExactPermissions[1], ["UpdateProcessArchitectureCommand"] = ExactPermissions[2], ["ArchiveProcessArchitectureCommand"] = ExactPermissions[3],
        ["CreateProcessDomainCommand"] = ExactPermissions[1], ["UpdateProcessDomainCommand"] = ExactPermissions[2], ["ArchiveProcessDomainCommand"] = ExactPermissions[3],
        ["CreateProcessFamilyCommand"] = ExactPermissions[1], ["UpdateProcessFamilyCommand"] = ExactPermissions[2], ["ArchiveProcessFamilyCommand"] = ExactPermissions[3],
        ["CreateProcessDefinitionCommand"] = ExactPermissions[5], ["UpdateProcessDefinitionCommand"] = ExactPermissions[6], ["ArchiveProcessDefinitionCommand"] = ExactPermissions[7],
        ["CreateProcessModelCommand"] = ExactPermissions[9], ["UpdateProcessModelCommand"] = ExactPermissions[10], ["UpdateDraftProcessModelVersionCommand"] = ExactPermissions[10],
        ["RequestProcessModelReviewCommand"] = ExactPermissions[11], ["ReturnProcessModelToDraftCommand"] = ExactPermissions[12],
        ["PublishProcessModelVersionCommand"] = ExactPermissions[13], ["RetireProcessModelVersionCommand"] = ExactPermissions[14], ["CreateProcessModelRevisionCommand"] = ExactPermissions[15]
    };
    public static IReadOnlyDictionary<string,string> ExactQueryMap { get; }=new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["ArchitectureDomainFamilyQueries"]=ExactPermissions[0], ["DefinitionQueries"]=ExactPermissions[4], ["ModelVersionHistoryGraphValidationQueries"]=ExactPermissions[8]
    };
}
