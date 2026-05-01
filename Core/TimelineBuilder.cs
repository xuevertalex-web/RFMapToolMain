namespace LocalCursorAgent.Core
{
    internal static class TimelineBuilder
    {
        public static Agent.TimelinePayload[] BuildMaxIterationsTimeline(int iterationsUsed, int maxIterations, string lastSuccessfulStep, string lastKnownAction)
        {
            var events = new List<Agent.TimelinePayload>
            {
                new()
                {
                    Stage = "IterationLoopStarted",
                    Status = "started",
                    Message = $"AgentIterationLoop started with max iterations {maxIterations}"
                }
            };

            for (var iteration = 1; iteration <= iterationsUsed; iteration++)
            {
                events.Add(new Agent.TimelinePayload
                {
                    Stage = "IterationStarted",
                    Status = "started",
                    Message = $"Iteration {iteration}/{maxIterations} started"
                });
                events.Add(new Agent.TimelinePayload
                {
                    Stage = "IterationCompleted",
                    Status = "completed",
                    Message = iteration == iterationsUsed
                        ? $"{lastSuccessfulStep}: {lastKnownAction}"
                        : $"Iteration {iteration}/{maxIterations} completed"
                });
            }

            events.Add(new Agent.TimelinePayload
            {
                Stage = "MaxIterationsReached",
                Status = "failed",
                Message = $"Iteration budget exhausted ({iterationsUsed}/{maxIterations})"
            });
            events.Add(new Agent.TimelinePayload
            {
                Stage = "RunFailedWithRootCause",
                Status = "failed",
                Message = "MAX_ITERATIONS_REACHED"
            });

            return events.ToArray();
        }

        public static Agent.TimelinePayload[] BuildAnalysisTimeline(bool modelTimedOut, bool fallbackUsed)
        {
            var events = new List<Agent.TimelinePayload>
            {
                new() { Stage = "TaskReceived", Status = "received", Message = "Task accepted for analysis" },
                new() { Stage = "IndexingStarted", Status = "started", Message = "Project indexing started" },
                new() { Stage = "IndexingCompleted", Status = "completed", Message = "Project indexing completed" },
                new() { Stage = "ModelCallStarted", Status = "started", Message = "Model call started" }
            };

            if (modelTimedOut)
            {
                events.Add(new Agent.TimelinePayload { Stage = "ModelCallTimedOut", Status = "timed_out", Message = "Model call timed out" });
            }

            if (fallbackUsed)
            {
                events.Add(new Agent.TimelinePayload { Stage = "AnalysisFallbackStarted", Status = "started", Message = "Using indexed context fallback" });
                events.Add(new Agent.TimelinePayload { Stage = "AnalysisFallbackCompleted", Status = "completed", Message = "Indexed context summary prepared" });
            }

            events.Add(new Agent.TimelinePayload
            {
                Stage = "RunCompleted",
                Status = "completed",
                Message = fallbackUsed ? "Run completed via fallback" : "Run completed"
            });

            return events.ToArray();
        }
    }
}
