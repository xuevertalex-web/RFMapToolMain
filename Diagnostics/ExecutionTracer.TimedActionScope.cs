namespace LocalCursorAgent.Diagnostics
{
    public partial class ExecutionTracer
    {
        private sealed class TimedActionScope : IDisposable
        {
            private readonly ExecutionTracer _tracer;
            private readonly string _eventType;
            private readonly string _component;
            private readonly ActionLogLevel _level;
            private readonly Dictionary<string, object?>? _metadata;
            private readonly string? _correlationId;
            private readonly DateTime _startedUtc;
            private bool _disposed;

            public TimedActionScope(
                ExecutionTracer tracer,
                string eventType,
                string component,
                ActionLogLevel level,
                Dictionary<string, object?>? metadata,
                string? correlationId)
            {
                _tracer = tracer;
                _eventType = eventType;
                _component = component;
                _level = level;
                _metadata = metadata;
                _correlationId = correlationId;
                _startedUtc = DateTime.UtcNow;
                _tracer.LogActionEvent($"{eventType}Started", component, level, "started", metadata: metadata, correlationId: correlationId);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _tracer.LogActionEvent(
                    $"{_eventType}Completed",
                    _component,
                    _level,
                    "completed",
                    metadata: _metadata,
                    durationMs: (long)(DateTime.UtcNow - _startedUtc).TotalMilliseconds,
                    correlationId: _correlationId);
            }
        }
    }
}
