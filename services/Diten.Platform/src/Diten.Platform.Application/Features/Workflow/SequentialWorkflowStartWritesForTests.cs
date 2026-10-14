namespace Diten.Platform.Application.Features.Workflow;

/// <summary>
/// WP-WF-ENGINE-RECOVERY-01 FIX3 item 2 — TESTS ONLY, BY NAME. A test host whose Mongo runs without transactions (a disposable
/// standalone mongod) registers this to let the generic start write its claim and rows one after another. Production never
/// registers it (a guard test reads the operator's container): there, a start without IPlatformTransactionExecutor refuses
/// to be built rather than write without a transaction.
/// </summary>
public sealed class SequentialWorkflowStartWritesForTests
{
}
