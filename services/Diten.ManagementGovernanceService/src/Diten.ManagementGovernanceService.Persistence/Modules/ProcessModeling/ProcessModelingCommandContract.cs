using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public static class ProcessModelingCommandContract
{
    private static readonly IReadOnlyDictionary<string,(string Collection,string Entity,string Mutation)> Map =
        new Dictionary<string,(string,string,string)>(StringComparer.Ordinal)
        {
            ["CreateProcessArchitectureCommand"]=("mg_process_architectures","ProcessArchitecture","Created"),
            ["UpdateProcessArchitectureCommand"]=("mg_process_architectures","ProcessArchitecture","Updated"),
            ["ArchiveProcessArchitectureCommand"]=("mg_process_architectures","ProcessArchitecture","Archived"),
            ["CreateProcessDomainCommand"]=("mg_process_domains","ProcessDomain","Created"),
            ["UpdateProcessDomainCommand"]=("mg_process_domains","ProcessDomain","Updated"),
            ["ArchiveProcessDomainCommand"]=("mg_process_domains","ProcessDomain","Archived"),
            ["CreateProcessFamilyCommand"]=("mg_process_families","ProcessFamily","Created"),
            ["UpdateProcessFamilyCommand"]=("mg_process_families","ProcessFamily","Updated"),
            ["ArchiveProcessFamilyCommand"]=("mg_process_families","ProcessFamily","Archived"),
            ["CreateProcessDefinitionCommand"]=("mg_process_definitions","ProcessDefinition","Created"),
            ["UpdateProcessDefinitionCommand"]=("mg_process_definitions","ProcessDefinition","Updated"),
            ["ArchiveProcessDefinitionCommand"]=("mg_process_definitions","ProcessDefinition","Archived"),
            ["CreateProcessModelCommand"]=("mg_process_models","ProcessModel","Created"),
            ["UpdateProcessModelCommand"]=("mg_process_models","ProcessModel","Updated"),
            ["UpdateDraftProcessModelVersionCommand"]=("mg_process_model_versions","ProcessModelVersion","DraftUpdated"),
            ["RequestProcessModelReviewCommand"]=("mg_process_model_versions","ProcessModelVersion","ReviewRequested"),
            ["ReturnProcessModelToDraftCommand"]=("mg_process_model_versions","ProcessModelVersion","ReturnedToDraft"),
            ["PublishProcessModelVersionCommand"]=("mg_process_model_versions","ProcessModelVersion","Published"),
            ["RetireProcessModelVersionCommand"]=("mg_process_model_versions","ProcessModelVersion","Retired"),
            ["CreateProcessModelRevisionCommand"]=("mg_process_model_versions","ProcessModelVersion","RevisionCreated")
        };

    public static string BusinessCollection(string command) => Get(command).Collection;
    public static (string Entity,string Mutation) Audit(string command)
    {
        var x=Get(command); return (x.Entity,x.Mutation);
    }
    private static (string Collection,string Entity,string Mutation) Get(string command)
    {
        if(!ProcessModelingPermissions.ExactCommandMap.ContainsKey(command) || !Map.TryGetValue(command,out var value))
            throw new InvalidOperationException("process_modeling_command_forbidden");
        return value;
    }
}
