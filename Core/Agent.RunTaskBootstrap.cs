using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class RunTaskBootstrapResult
        {
            public required bool IsRejected { get; init; }
            public string? RejectedResult { get; init; }
            public required DateTime RunStartedUtc { get; init; }
            public required ExecutionTracer Tracer { get; init; }
            public required string? RequestedNewFile { get; init; }
        }

        private RunTaskBootstrapResult PrepareRunTaskBootstrap(string task)
        {
            var runStartedUtc = DateTime.UtcNow;
            _memory.Add("task_start", task);
            var tracer = _contextBuilder.Tracer;
            var requestedNewFile = NewFilePathExtractor.ExtractRequestedNewFilePath(task);
            tracer.LogActionEvent("TaskReceived", "Agent", ExecutionTracer.ActionLogLevel.Info, "received", metadata: new Dictionary<string, object?>
            {
                { "task", task }
            });
            tracer.LogActionEvent("TaskLifecycle", "Agent", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "task", task },
                { "requested_new_file", requestedNewFile ?? string.Empty }
            });

            if (TryRejectTaskBeforeExecution(task, tracer, out var precheckResult))
            {
                return new RunTaskBootstrapResult
                {
                    IsRejected = true,
                    RejectedResult = precheckResult,
                    RunStartedUtc = runStartedUtc,
                    Tracer = tracer,
                    RequestedNewFile = requestedNewFile
                };
            }

            return new RunTaskBootstrapResult
            {
                IsRejected = false,
                RunStartedUtc = runStartedUtc,
                Tracer = tracer,
                RequestedNewFile = requestedNewFile
            };
        }
    }
}
