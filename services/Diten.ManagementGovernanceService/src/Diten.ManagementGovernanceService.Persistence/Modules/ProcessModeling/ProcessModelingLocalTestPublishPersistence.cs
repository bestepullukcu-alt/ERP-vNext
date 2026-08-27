using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Publish;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Approval;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public sealed class ProcessModelingLocalTestPublishTargetReader(ProcessModelingMongoContext context):ILocalTestPublishTargetReader
{
    public async Task<LocalTestPublishTargetSnapshot?> FindAsync(Guid tenantId,Guid processModelVersionId,CancellationToken cancellationToken)
    {
        var versionFilter=Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),
            Builders<BsonDocument>.Filter.Eq("_id",G(processModelVersionId)),
            Builders<BsonDocument>.Filter.Eq("IsDeleted",false));
        var version=await context.Collection("mg_process_model_versions").WithReadConcern(ReadConcern.Majority).Find(versionFilter).FirstOrDefaultAsync(cancellationToken);
        if(version is null||!version.TryGetValue("ProcessModelId",out var modelRaw)||modelRaw.BsonType!=BsonType.Binary||
           !version.TryGetValue("ModelAuthorActorId",out var authorRaw)||authorRaw.BsonType!=BsonType.Binary||
           !version.TryGetValue("ContentHash",out var hashRaw)||!hashRaw.IsString||
           !version.TryGetValue("Version",out var versionRaw)||!versionRaw.IsInt32||
           !version.TryGetValue("LifecycleState",out var lifecycleRaw)||!lifecycleRaw.IsString)
            return null;
        var modelId=ProcessModelingMongoGuidPolicy.Read(modelRaw);
        var modelFilter=Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),
            Builders<BsonDocument>.Filter.Eq("_id",G(modelId)),
            Builders<BsonDocument>.Filter.Eq("IsDeleted",false));
        if(!await context.Collection("mg_process_models").WithReadConcern(ReadConcern.Majority).Find(modelFilter).AnyAsync(cancellationToken))return null;
        return new(tenantId,modelId,processModelVersionId,hashRaw.AsString,ProcessModelingMongoGuidPolicy.Read(authorRaw),versionRaw.AsInt32,lifecycleRaw.AsString);
    }
    private static BsonBinaryData G(Guid value)=>ProcessModelingMongoGuidPolicy.Write(value);
}

public sealed class ProcessModelingLocalTestPublishExecutor(
    ILocalTestPublishTargetReader targets,
    ILocalTestPublishAuthorizationEvidenceProvider authorization,
    ProcessModelingMongoMutationWriter writer,
    ProcessModelingMongoReceiptReader receipts,
    ProcessModelingMongoTransactionRunner transactions,
    TimeProvider timeProvider):IProcessModelingLocalCommandExecutor
{
    public async Task<ProcessModelingLocalExecutionResult> ExecuteAsync(ProcessModelingLocalExecutionRequest command,CancellationToken cancellationToken)
    {
        if(!string.Equals(command.CommandName,"PublishProcessModelVersionCommand",StringComparison.Ordinal))
            return ProcessModelingLocalExecutionResult.Fail("process_modeling_local_test_executor_unavailable",503);
        if(command.RouteId is not { } versionId||versionId==Guid.Empty||command.ExpectedVersion is not { } expectedVersion||expectedVersion<0)
            return ProcessModelingLocalExecutionResult.Fail(PublishApprovalFailureCodes.InvalidRequest,400);
        if(!string.Equals(command.Context.Permission,ProcessModelingPermissions.ExactCommandMap[command.CommandName],StringComparison.Ordinal))
            return ProcessModelingLocalExecutionResult.Fail(PublishApprovalFailureCodes.PermissionDenied,403);

        try
        {
            var target=await targets.FindAsync(command.Context.TenantId,versionId,cancellationToken);
            if(target is null)return ProcessModelingLocalExecutionResult.Fail(PublishApprovalFailureCodes.ResourceNotFound,404);
            var initial=await ResolveEvidenceAsync(target,command.Context,cancellationToken);
            var initialResult=SafeEvaluate(target,command.Context,initial,timeProvider.GetUtcNow().UtcDateTime);
            if(!initialResult.IsExecutable)return ProcessModelingLocalExecutionResult.Fail(initialResult.StableCode,initialResult.HttpStatus);

            var request=new CoreMutationRequest(command.Context.TenantId,initial.Input.InitialRequest.PublisherActorId,
                command.CommandName,command.Context.IdempotencyKey,"sha256:"+command.Context.RequestHash,expectedVersion,versionId);
            var durable=await receipts.ReconcileAsync(request,true,cancellationToken);
            if(durable is not null)return ToLocal(durable);
            if(target.Version!=expectedVersion||!string.Equals(target.LifecycleState,"Review",StringComparison.Ordinal))
                return ProcessModelingLocalExecutionResult.Fail(ProcessModelingErrors.StaleVersion,409);

            var now=timeProvider.GetUtcNow().UtcDateTime;
            var envelope=new MongoMutationEnvelope(request,new BsonDocument(),"published",Guid.NewGuid(),Guid.NewGuid(),now,
                "fu16:"+initial.Input.InitialRequest.PublisherActorId.ToString("D").ToLowerInvariant()+":"+initial.CompositeFence,204,versionId,target.ContentHash);
            var result=await transactions.ExecuteAsync(async(session,ct)=>
            {
                // The full authoritative tuple is re-read inside the same body and before the first
                // business/receipt/audit/outbox write. No participant can precede this fence.
                var revalidated=await RevalidateEvidenceAsync(target,command.Context,initial.CompositeFence,ct);
                var binding=SafeBindings(revalidated,target,command.Context);
                if(!binding.IsExecutable)throw new LocalTestPublishAuthorizationException(binding.HttpStatus,binding.StableCode);
                var finalResult=SafeRevalidate(initial,revalidated,timeProvider.GetUtcNow().UtcDateTime);
                if(!finalResult.IsExecutable)throw new LocalTestPublishAuthorizationException(finalResult.HttpStatus,finalResult.StableCode);
                await writer.WriteAsync(session,envelope,ct);
                return new CoreMutationResult(true,204,"published",versionId);
            },ct=>receipts.ReconcileAsync(request,true,ct),cancellationToken);
            return ToLocal(result);
        }
        catch(LocalTestPublishAuthorizationException ex){return ProcessModelingLocalExecutionResult.Fail(ex.StableCode,ex.StatusCode);}
        catch(ProcessModelingNotFoundException ex){return ProcessModelingLocalExecutionResult.Fail(ex.Message,404);}
        catch(ProcessModelingConflictException ex){return ProcessModelingLocalExecutionResult.Fail(ex.Message,409);}
        catch(ProcessModelingUnavailableException ex){return ProcessModelingLocalExecutionResult.Fail(ex.Message,503);}
        catch(MongoException){return ProcessModelingLocalExecutionResult.Fail(ProcessModelingErrors.ProviderUnavailable,503);}
    }

    private async Task<LocalTestPublishAuthorizationEvidence> ResolveEvidenceAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext context,CancellationToken ct)
    {
        try{return (await authorization.ResolveAsync(target,context,ct)).Validate();}
        catch(OperationCanceledException){throw;}
        catch(LocalTestPublishAuthorizationException){throw;}
        catch(Exception){throw new LocalTestPublishAuthorizationException(503,PublishApprovalFailureCodes.AuthorityUnavailable);}
    }

    private async Task<LocalTestPublishAuthorizationEvidence> RevalidateEvidenceAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext context,string fence,CancellationToken ct)
    {
        try{return (await authorization.RevalidateAsync(target,context,fence,ct)).Validate();}
        catch(OperationCanceledException){throw;}
        catch(LocalTestPublishAuthorizationException){throw;}
        catch(Exception){throw new LocalTestPublishAuthorizationException(503,PublishApprovalFailureCodes.AuthorityUnavailable);}
    }

    private static LocalTestPublishGateResult Evaluate(LocalTestPublishTargetSnapshot target,LocalTestCommandContext context,LocalTestPublishAuthorizationEvidence evidence,DateTime evaluatedAtUtc)
    {
        var binding=LocalTestNotRequiredPublishGate.ValidateBindings(evidence,target,context);
        return binding.IsExecutable?LocalTestNotRequiredPublishGate.Evaluate(evidence,evaluatedAtUtc):binding;
    }

    private static LocalTestPublishGateResult SafeEvaluate(LocalTestPublishTargetSnapshot target,LocalTestCommandContext context,LocalTestPublishAuthorizationEvidence evidence,DateTime now)
    {
        try{return Evaluate(target,context,evidence,now);}
        catch(Exception ex) when(ex is ArgumentException or NullReferenceException or FormatException)
        {throw new LocalTestPublishAuthorizationException(503,PublishApprovalFailureCodes.AuthorityUnavailable);}
    }
    private static LocalTestPublishGateResult SafeBindings(LocalTestPublishAuthorizationEvidence evidence,LocalTestPublishTargetSnapshot target,LocalTestCommandContext context)
    {
        try{return LocalTestNotRequiredPublishGate.ValidateBindings(evidence,target,context);}
        catch(Exception ex) when(ex is ArgumentException or NullReferenceException or FormatException)
        {throw new LocalTestPublishAuthorizationException(503,PublishApprovalFailureCodes.AuthorityUnavailable);}
    }
    private static LocalTestPublishGateResult SafeRevalidate(LocalTestPublishAuthorizationEvidence initial,LocalTestPublishAuthorizationEvidence revalidated,DateTime now)
    {
        try{return LocalTestNotRequiredPublishGate.Revalidate(initial,revalidated,now);}
        catch(Exception ex) when(ex is ArgumentException or NullReferenceException or FormatException)
        {throw new LocalTestPublishAuthorizationException(503,PublishApprovalFailureCodes.AuthorityUnavailable);}
    }

    private static ProcessModelingLocalExecutionResult ToLocal(CoreMutationResult result)=>result.Accepted
        ? new(true,result.HttpStatus,result.StableCode,result.AggregateId)
        : ProcessModelingLocalExecutionResult.Fail(result.StableCode,result.HttpStatus);
}
