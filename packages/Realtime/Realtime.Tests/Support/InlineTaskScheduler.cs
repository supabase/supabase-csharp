using System.Collections.Generic;
using System.Threading.Tasks;

namespace Realtime.Tests.Support;

/// <summary>
///     Runs every task on the thread that starts it, so whatever a call hands to a task has finished by the time the call returns.
/// </summary>
internal sealed class InlineTaskScheduler : TaskScheduler
{
    protected override void QueueTask(Task task) => this.TryExecuteTask(task);

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => this.TryExecuteTask(task);

    protected override IEnumerable<Task>? GetScheduledTasks() => null;
}
